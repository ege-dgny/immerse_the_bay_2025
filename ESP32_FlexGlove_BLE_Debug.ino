#include <BLEDevice.h>
#include <BLEUtils.h>
#include <BLEScan.h>
#include <BLEAdvertisedDevice.h>

// ESP32 Flex Sensor Glove -> Unity via BLE (Debug Version)
// This version has more error checking and debugging

#include <BLEDevice.h>
#include <BLEServer.h>
#include <BLEUtils.h>
#include <BLE2902.h>
#include <string.h>

// Flex sensor pin definitions
#define FLEX_THUMB   36
#define FLEX_INDEX   39
#define FLEX_MIDDLE  34
#define FLEX_RING    35
#define FLEX_PINKY   32

// BLE UUIDs (randomly generated)
#define SERVICE_UUID        "a7f3c9e1-4b2d-8f6a-1c3e-9d5b7a2f4e8c"
#define CHARACTERISTIC_UUID "d8e4f2a6-3c1b-7e9d-2a4f-6c8b1e3d5a7f"

BLEServer* pServer = NULL;
BLECharacteristic* pCharacteristic = NULL;
bool deviceConnected = false;

class MyServerCallbacks: public BLEServerCallbacks {
    void onConnect(BLEServer* pServer) {
      deviceConnected = true;
      Serial.println("*** Device Connected ***");
    }
    void onDisconnect(BLEServer* pServer) {
      deviceConnected = false;
      Serial.println("*** Device Disconnected ***");
      // Restart advertising after disconnect
      Serial.println("Restarting advertising...");
      BLEDevice::startAdvertising();
      Serial.println("Advertising restarted - waiting for connection...");
    }
};

void setup() {
  // Initialize Serial FIRST
  Serial.begin(115200);
  delay(2000);  // Give Serial time to initialize
  
  Serial.println("\n\n=================================");
  Serial.println("ESP32 BLE Flex Glove Starting...");
  Serial.println("=================================\n");
  
  // Check if BLE is available
  if (!BLEDevice::getInitialized()) {
    Serial.println("Initializing BLE...");
    BLEDevice::init("FlexGlove-ESP32");
    
    // Set maximum BLE power for better range
    Serial.println("Setting BLE power to maximum...");
    BLEDevice::setPower(ESP_PWR_LVL_P9);
    
    Serial.println("BLE initialized with max power");
  } else {
    Serial.println("BLE already initialized");
  }
  
  // Create BLE Server
  Serial.println("Creating BLE server...");
  pServer = BLEDevice::createServer();
  if (pServer == NULL) {
    Serial.println("ERROR: Failed to create BLE server!");
    while(1) delay(1000); // Stop here
  }
  Serial.println("BLE server created");
  
  pServer->setCallbacks(new MyServerCallbacks());
  Serial.println("Callbacks set");

  // Create BLE Service
  Serial.println("Creating BLE service...");
  BLEService *pService = pServer->createService(BLEUUID(SERVICE_UUID));
  if (pService == NULL) {
    Serial.println("ERROR: Failed to create BLE service!");
    while(1) delay(1000); // Stop here
  }
  Serial.println("BLE service created");
  
  // Create BLE Characteristic
  Serial.println("Creating BLE characteristic...");
  pCharacteristic = pService->createCharacteristic(
    BLEUUID(CHARACTERISTIC_UUID),
    BLECharacteristic::PROPERTY_READ |
    BLECharacteristic::PROPERTY_NOTIFY
  );
  if (pCharacteristic == NULL) {
    Serial.println("ERROR: Failed to create BLE characteristic!");
    while(1) delay(1000); // Stop here
  }
  Serial.println("BLE characteristic created");

  // Add descriptor for notifications
  Serial.println("Adding descriptor...");
  pCharacteristic->addDescriptor(new BLE2902());
  Serial.println("Descriptor added");
  
  // Start the service
  Serial.println("Starting service...");
  pService->start();
  Serial.println("Service started");

  // Configure advertising
  Serial.println("Configuring advertising...");
  BLEAdvertising *pAdvertising = BLEDevice::getAdvertising();
  if (pAdvertising == NULL) {
    Serial.println("ERROR: Failed to get advertising object!");
    while(1) delay(1000); // Stop here
  }
  Serial.println("Got advertising object");
  
  // IMPORTANT: NOT adding service UUID to advertising for better discoverability
  // Many BLE scanners filter out custom UUIDs
  Serial.println("SKIPPING service UUID in advertising (improves discoverability)");
  // pAdvertising->addServiceUUID(BLEUUID(SERVICE_UUID));  // COMMENTED OUT
  
  // Set scan response to include more data
  Serial.println("Setting scan response...");
  pAdvertising->setScanResponse(true);
  Serial.println("Scan response set");
  
  // Set advertising parameters for better discoverability
  Serial.println("Setting advertising parameters...");
  pAdvertising->setMinPreferred(0x06);  // helps with iPhone connections
  pAdvertising->setMaxPreferred(0x12);  // helps with iPhone connections
  
  // Set explicit advertising intervals for better visibility
  pAdvertising->setMinInterval(0x20);  // 20ms * 0.625 = 12.5ms
  pAdvertising->setMaxInterval(0x40);  // 40ms * 0.625 = 25ms
  Serial.println("Advertising intervals set (12.5ms - 25ms)");
  
  Serial.println("Advertising parameters set");
  
  // Start advertising
  Serial.println("Starting advertising...");
  BLEDevice::startAdvertising();
  Serial.println("Advertising started!");
  
  delay(2000); // Give advertising more time to start and stabilize
  
  Serial.println("\n=================================");
  Serial.println("BLE READY!");
  Serial.println("Device name: FlexGlove-ESP32");
  Serial.println("Service UUID: " + String(SERVICE_UUID));
  Serial.println("Characteristic UUID: " + String(CHARACTERISTIC_UUID));
  Serial.println("Advertising: ACTIVE");
  Serial.println("BLE Power: MAXIMUM");
  Serial.println("Waiting for connection...");
  Serial.println("=================================\n");
  
  Serial.println("TROUBLESHOOTING CHECKLIST:");
  Serial.println("✓ Service UUID removed from advertising");
  Serial.println("✓ BLE power set to maximum");
  Serial.println("✓ Advertising intervals optimized");
  Serial.println("");
  Serial.println("TO FIND THIS DEVICE:");
  Serial.println("1. Enable Bluetooth on your phone");
  Serial.println("2. Open BLE scanner app (nRF Connect recommended)");
  Serial.println("3. Pull down to refresh/restart scan");
  Serial.println("4. Look for: 'FlexGlove-ESP32'");
  Serial.println("5. Device should appear within 2-3 seconds");
  Serial.println("");
  Serial.println("Status updates every 5 seconds...");
  Serial.println("");
}

void loop() {
  // Status update every 5 seconds (for serial monitor)
  static unsigned long lastStatusPrint = 0;
  static int counter = 0;
  
  // Data send rate when connected (adjust this value to change rate)
  static unsigned long lastDataSend = 0;
  const unsigned long DATA_SEND_INTERVAL = 50;  // Send data every 50ms (20 Hz)
  
  if (deviceConnected) {
    // Send sensor data at high frequency
    unsigned long currentTime = millis();
    if (currentTime - lastDataSend >= DATA_SEND_INTERVAL) {
      lastDataSend = currentTime;
      
      // Read all flex sensors
      String testData = "Flex: T:" + String(analogRead(FLEX_THUMB)) + 
                       " I:" + String(analogRead(FLEX_INDEX)) +
                       " M:" + String(analogRead(FLEX_MIDDLE)) +
                       " R:" + String(analogRead(FLEX_RING)) +
                       " P:" + String(analogRead(FLEX_PINKY));
      
      // Send via BLE
      pCharacteristic->setValue(testData.c_str());
      pCharacteristic->notify();
      
      // Print status every 5 seconds to avoid serial spam
      if (currentTime - lastStatusPrint > 5000) {
        lastStatusPrint = currentTime;
        counter++;
        Serial.print("[");
        Serial.print(counter * 5);
        Serial.print("s] *** CONNECTED *** Sending data at ~");
        Serial.print(1000 / DATA_SEND_INTERVAL);
        Serial.println(" Hz");
        Serial.println("  Latest: " + testData);
      }
    }
    delay(10);  // Small delay to prevent overwhelming the system
    
  } else {
    // Not connected - status update every 5 seconds
    unsigned long currentTime = millis();
    if (currentTime - lastStatusPrint > 5000) {
      lastStatusPrint = currentTime;
      counter++;
      Serial.print("[");
      Serial.print(counter * 5);
      Serial.println("s] ADVERTISING - Waiting for BLE connection...");
      Serial.println("  Tip: If not visible after 30s, power cycle ESP32");
    }
    delay(1000);
  }
}