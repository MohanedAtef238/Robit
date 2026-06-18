#include <esp_now.h>
#include <WiFi.h>
#include "CheezsEMG.h"

// MAC Address of the receiver dongle (You MUST update this!)
uint8_t receiverAddress[] = {0x94, 0xA9, 0x90, 0x7B, 0x63, 0x24}; 

// --- PIN SELECTION (ESP32-C3 Supermini right side) ---
#define INPUT_PIN 3      // Signal input - GPIO3
#define DETECT_PIN 4     // Detect input - GPIO4
#define SAMPLE_RATE 500  // Sampling rate in Hz

CheezsEMG sEMG(INPUT_PIN, DETECT_PIN, SAMPLE_RATE);  

// --- Smoothing & Scaling Variables ---
#define ENVELOPE_WINDOW 20    
#define FILTER_WINDOW 20    

float envelopeBuffer[ENVELOPE_WINDOW];
float filterBuffer[FILTER_WINDOW];
int bufferIndex = 0; 

float envMin = 4095;
float envMax = 0;
float filtMin = 4095;
float filtMax = 0;

// --- Data Structure (No Batching, sent 500 times a second) ---
typedef struct struct_message {
    float scaled_filtered;
    float scaled_envelope;
    int detect;
    uint32_t packet_id;
} struct_message;

struct_message outgoingData;
esp_now_peer_info_t peerInfo;

uint32_t currentPacketId = 0;

void OnDataSent(const esp_now_send_info_t *tx_info, esp_now_send_status_t status) {
    // Keep this empty. Since we are sending 500 times a second, 
    // printing to Serial here would crash the ESP32!
}

void setup() {
    Serial.begin(115200);
    
    WiFi.mode(WIFI_STA);
    if (esp_now_init() != ESP_OK) {
        Serial.println("Error initializing ESP-NOW");
        return;
    }
    
    esp_now_register_send_cb(OnDataSent);
    
    memcpy(peerInfo.peer_addr, receiverAddress, 6);
    peerInfo.channel = 0;  
    peerInfo.encrypt = false;
    
    if (esp_now_add_peer(&peerInfo) != ESP_OK){
        Serial.println("Failed to add peer");
        return;
    }

    sEMG.begin();

    // Initialize buffers
    for (int i = 0; i < ENVELOPE_WINDOW; i++) envelopeBuffer[i] = 0;
    for (int i = 0; i < FILTER_WINDOW; i++) filterBuffer[i] = 0;
}

void loop() {
    if (sEMG.checkSampleInterval()) {
        sEMG.processSignal();  

        // 1. Get raw library values
        float env = sEMG.getEnvelopeSignal();
        float filt = sEMG.getFilteredSignal();

        // 2. Update circular buffers
        envelopeBuffer[bufferIndex] = env;
        filterBuffer[bufferIndex] = filt;
        bufferIndex = (bufferIndex + 1) % ENVELOPE_WINDOW;

        // 3. Compute smoothed values (Moving Average)
        float smoothEnv = 0;
        float smoothFilt = 0;
        for (int i = 0; i < ENVELOPE_WINDOW; i++) {
            smoothEnv += envelopeBuffer[i];
            smoothFilt += filterBuffer[i];
        }
        smoothEnv /= ENVELOPE_WINDOW;
        smoothFilt /= FILTER_WINDOW;

        // 4. Update auto-scale 
        if (smoothFilt < filtMin) filtMin = smoothFilt;
        if (smoothFilt > filtMax) filtMax = smoothFilt;
        if (smoothEnv < envMin) envMin = smoothEnv;
        if (smoothEnv > envMax) envMax = smoothEnv;

        // 5. Scale both to 0-1023
        float scaledEnv = (envMax > envMin) ? map(smoothEnv, envMin, envMax, 0, 1023) : 0;
        float scaledFilt = (filtMax > filtMin) ? map(smoothFilt, filtMin, filtMax, 0, 1023) : 0;

        // Store and send IMMEDIATELY
        outgoingData.scaled_filtered = scaledFilt;
        outgoingData.scaled_envelope = scaledEnv;
        outgoingData.detect = sEMG.getDetectSignal();
        outgoingData.packet_id = currentPacketId++;
        
        esp_now_send(receiverAddress, (uint8_t *) &outgoingData, sizeof(outgoingData));
    }
}
