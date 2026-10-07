namespace PoultryPro.Models.ViewModels
{
    /// <summary>Code samples shown on Sensors/ApiDocs (kept in C# so Razor never parses the angle brackets).</summary>
    public static class SensorSnippets
    {
        public static string JsonSchema => """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "title": "PoultryPro sensor reading",
              "type": "object",
              "required": ["deviceCode"],
              "properties": {
                "deviceCode":      { "type": "string",  "maxLength": 40, "description": "Code registered in PoultryPro" },
                "temperatureC":    { "type": "number",  "minimum": -20, "maximum": 70 },
                "humidityPercent": { "type": "number",  "minimum": 0,   "maximum": 100 },
                "ammoniaPpm":      { "type": "number",  "minimum": 0,   "maximum": 500 },
                "co2Ppm":          { "type": "number",  "minimum": 0,   "maximum": 50000 },
                "lightLux":        { "type": "number",  "minimum": 0,   "maximum": 200000 },
                "waterFlowLtr":    { "type": "number",  "minimum": 0,   "description": "Litres since the previous reading" },
                "feedLevelPct":    { "type": "number",  "minimum": 0,   "maximum": 100 },
                "readingTime":     { "type": "string",  "format": "date-time", "description": "Optional; server time is used when omitted. Last 30 days only." }
              },
              "minProperties": 2
            }
            """;

        public static string Response => """
            HTTP/1.1 200 OK
            {
              "ok": true,
              "id": 18342,
              "deviceCode": "PP-NODE-001",
              "thi": 81.6,
              "band": "Alert",
              "receivedAt": "2026-10-07T14:05:12"
            }

            400  { "ok": false, "error": "Invalid reading.", "details": ["humidityPercent must be between 0 and 100."] }
            401  { "ok": false, "error": "Unknown device or invalid device key." }
            403  { "ok": false, "error": "Device is deactivated." }
            """;

        public static string Curl(string baseUrl, string deviceCode, string apiKey) => $$"""
            curl -X POST "{{baseUrl}}/api/sensors/ingest" \
              -H "Content-Type: application/json" \
              -H "X-Device-Key: {{apiKey}}" \
              -d '{"deviceCode":"{{deviceCode}}","temperatureC":29.4,"humidityPercent":72.5,"ammoniaPpm":14.2,"co2Ppm":1450,"lightLux":55,"waterFlowLtr":42.5,"feedLevelPct":68}'

            # Read back the latest stored value
            curl "{{baseUrl}}/api/sensors/{{deviceCode}}/latest" -H "X-Device-Key: {{apiKey}}"
            """;

        public static string Esp32Sketch(string baseUrl, string deviceCode, string apiKey) => $$"""
            /*
             * PoultryPro — ESP32 shed climate node
             * Hardware : ESP32 DevKit v1 + DHT22 (AM2302) on GPIO 4 (10k pull-up to 3V3)
             * Optional : MQ-137 ammonia sensor on GPIO 34 (ADC), LDR on GPIO 35
             * Libraries: "DHT sensor library" + "Adafruit Unified Sensor" (Library Manager)
             * Posts one JSON reading every 5 minutes to PoultryPro.
             */
            #include <WiFi.h>
            #include <HTTPClient.h>
            #include <DHT.h>

            // ---------- configuration ----------
            const char* WIFI_SSID     = "YOUR_WIFI_SSID";
            const char* WIFI_PASSWORD = "YOUR_WIFI_PASSWORD";
            const char* SERVER_URL    = "{{baseUrl}}/api/sensors/ingest";
            const char* DEVICE_CODE   = "{{deviceCode}}";
            const char* DEVICE_KEY    = "{{apiKey}}";

            #define DHT_PIN        4
            #define DHT_TYPE       DHT22
            #define MQ137_PIN      34      // analog; leave unconnected if not fitted
            #define LDR_PIN        35      // analog; leave unconnected if not fitted
            #define HAS_MQ137      false
            #define HAS_LDR        false
            const unsigned long INTERVAL_MS = 5UL * 60UL * 1000UL;   // 5 minutes

            DHT dht(DHT_PIN, DHT_TYPE);
            unsigned long lastPost = 0;

            void connectWiFi() {
              if (WiFi.status() == WL_CONNECTED) return;
              Serial.printf("Connecting to %s", WIFI_SSID);
              WiFi.mode(WIFI_STA);
              WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
              int tries = 0;
              while (WiFi.status() != WL_CONNECTED && tries++ < 40) { delay(500); Serial.print('.'); }
              Serial.println(WiFi.status() == WL_CONNECTED ? " connected" : " FAILED");
              if (WiFi.status() == WL_CONNECTED) Serial.println(WiFi.localIP());
            }

            // Rough MQ-137 conversion (calibrate Rs/R0 for your sensor in clean air first)
            float readAmmoniaPpm() {
              int raw = analogRead(MQ137_PIN);
              float volts = raw * (3.3f / 4095.0f);
              if (volts < 0.05f) return 0;
              float rs = (3.3f - volts) / volts * 10.0f;   // 10k load resistor
              const float R0 = 30.0f;
              return 102.2f * powf(rs / R0, -2.473f);
            }

            float readLux() {
              int raw = analogRead(LDR_PIN);
              return raw * (1000.0f / 4095.0f);            // approximate; calibrate with a lux meter
            }

            bool postReading() {
              float t = dht.readTemperature();
              float h = dht.readHumidity();
              if (isnan(t) || isnan(h)) { Serial.println("DHT22 read failed"); return false; }

              char body[320];
              int n = snprintf(body, sizeof(body),
                "{\"deviceCode\":\"%s\",\"temperatureC\":%.2f,\"humidityPercent\":%.2f",
                DEVICE_CODE, t, h);
              if (HAS_MQ137) n += snprintf(body + n, sizeof(body) - n, ",\"ammoniaPpm\":%.2f", readAmmoniaPpm());
              if (HAS_LDR)   n += snprintf(body + n, sizeof(body) - n, ",\"lightLux\":%.1f", readLux());
              snprintf(body + n, sizeof(body) - n, "}");

              HTTPClient http;
              http.begin(SERVER_URL);                      // for https use WiFiClientSecure + setCACert()
              http.addHeader("Content-Type", "application/json");
              http.addHeader("X-Device-Key", DEVICE_KEY);
              http.setTimeout(10000);
              int code = http.POST((uint8_t*)body, strlen(body));
              String resp = http.getString();
              http.end();

              Serial.printf("POST %d  %s\n  -> %s\n", code, body, resp.c_str());
              return code == 200;
            }

            void setup() {
              Serial.begin(115200);
              delay(200);
              dht.begin();
              analogReadResolution(12);
              connectWiFi();
              delay(2000);                                 // DHT22 needs ~2 s after power-up
              postReading();
              lastPost = millis();
            }

            void loop() {
              if (millis() - lastPost >= INTERVAL_MS) {
                connectWiFi();
                if (!postReading()) {                      // one retry after 30 s on failure
                  delay(30000);
                  postReading();
                }
                lastPost = millis();
              }
              delay(1000);
            }
            """;
    }
}
