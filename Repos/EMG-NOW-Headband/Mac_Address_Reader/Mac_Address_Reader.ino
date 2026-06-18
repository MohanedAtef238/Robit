#include <WiFi.h>

void setup() {
  Serial.begin(115200);
  
  // Wait a moment for the serial monitor to catch up
  delay(1000);
  
  Serial.println("\n--- ESP32 MAC Address Reader ---");
  
  // Set WiFi to station mode
  WiFi.mode(WIFI_STA);
  
  // Get and print the MAC address
  String macAddress = WiFi.macAddress();
  Serial.print("This Board's MAC Address: ");
  Serial.println(macAddress);
  
  Serial.println("\n--------------------------------");
  Serial.println("INSTRUCTIONS:");
  Serial.println("1. Copy the MAC address above.");
  Serial.println("2. Paste it into the Sender.ino file where indicated.");
  Serial.println("--------------------------------");
}

void loop() {
  // Nothing to do here
}
