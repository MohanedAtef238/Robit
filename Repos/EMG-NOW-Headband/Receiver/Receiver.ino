#include <esp_now.h>
#include <WiFi.h>

// --- Data Structure (No Batching) ---
typedef struct struct_message {
    float scaled_filtered;
    float scaled_envelope;
    int detect;
    uint32_t packet_id;
} struct_message;

struct_message incomingData;

// --- Connection Status Variables ---
unsigned long lastPacketTime = 0;
const int LED_PIN = 8; // The built-in blue LED on the SuperMini-C3

// Callback function that will be executed when data is received
void OnDataRecv(const esp_now_recv_info_t *esp_now_info, const uint8_t *incomingDataPtr, int len) {
    if (len == sizeof(incomingData)) {
        memcpy(&incomingData, incomingDataPtr, sizeof(incomingData));
        
        // Mark the time we received this packet
        lastPacketTime = millis();
        
        // Turn the built-in LED ON (It is "Active Low" on this board, so LOW means ON)
        digitalWrite(LED_PIN, LOW);

        // Print the exact two values expected by unity_emg_bridge.exe
        Serial.println(
            String(incomingData.scaled_filtered) + "," + 
            String(incomingData.scaled_envelope)
        );
        
    } else {
        // Only print errors if we really need to, to avoid clogging the .exe
    }
}

void setup() {
    Serial.begin(115200);
    WiFi.mode(WIFI_STA);

    pinMode(LED_PIN, OUTPUT);
    digitalWrite(LED_PIN, HIGH); // Start with LED OFF

    if (esp_now_init() != ESP_OK) {
        return;
    }
    
    esp_now_register_recv_cb(OnDataRecv);
    
    // No CSV header here. The unity_emg_bridge.exe expects raw floats immediately.
}

void loop() {
    // Watchdog: If 1 second passes without receiving any data, turn the LED OFF
    if (millis() - lastPacketTime > 1000) {
        digitalWrite(LED_PIN, HIGH); 
    }
    
    delay(10);
}
