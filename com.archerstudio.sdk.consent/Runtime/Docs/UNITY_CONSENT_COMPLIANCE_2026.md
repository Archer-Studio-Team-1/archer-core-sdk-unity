# HƯỚNG DẪN TRIỂN KHAI CONSENT CHUẨN 2026 (TCF V2.3 & DMA)
**Dự án:** Idle Dungeon Keeper Tycoon RPG  
**Nền tảng:** Unity 6000+ (Android & iOS)  
**Công nghệ:** Google UMP, AppLovin MAX, Facebook SDK, Adjust, Firebase.

---

## 1. TỔNG QUAN LUỒNG LOGIC (ARCHITECTURE)

Hệ thống tuân thủ nguyên tắc: **UMP là nguồn sự thật duy nhất (Single Source of Truth)**. Mọi SDK khác chỉ được khởi tạo hoặc thu thập dữ liệu sau khi có tín hiệu từ UMP.

### 1.1. Sơ đồ thực thi (Execution Flow)
1. **Khởi chạy App:** Cấu hình mặc định cho Firebase/Facebook trong Manifest/Plist.
2. **UMP Update:** Gọi `ConsentInformation.Update()` để kiểm tra vùng địa lý (EEA/Global).
3. **Show Form:** Hiển thị bảng Consent (nếu cần). User chọn "Accept All" hoặc "Manage Options".
4. **Trích xuất Tín hiệu:** Đọc consent mode values từ UMP SharedPreferences.
5. **Cập nhật SDK Partner:**
  - Set `Consent Mode v2` cho Firebase Analytics (`setConsent()` API).
  - Set `LDU` cho Facebook.
  - Set `ThirdPartySharing` + DMA params cho Adjust.
  - Set `SetHasUserConsent` + `facebook_limited_data_use` cho MAX.
6. **Khởi tạo Mediation:** Gọi `MaxSdk.InitializeSdk()`.

### 1.2. Module Priority Order
```
Priority 0:  ConsentManager (UMP/MAX consent provider)
Priority 10: FirebaseModule (Firebase init + Consent Mode v2)
Priority 20: TrackingModule (Adjust + Firebase tracking)
Priority 30: FacebookModule (FB.Init + LDU/ATT)
Priority 50: AdManager (MAX mediation init)
```

---

## 2. CẤU HÌNH DASHBOARD (GỐC)

### 2.1. AdMob UMP Dashboard
- **GDPR Message:** Bật 3 nút: *Consent, Do not consent, Manage options*.
- **Custom Vendor Selection:** (Bắt buộc) Thêm thủ công:
  - **Meta (Facebook):** ID 31.
  - **AppLovin:** ID 311 (Additional Consent).
  - **Adjust:** Thêm vào danh sách đối tác đo lường.
  - **Google Advertising Products:** ID 755.
  - **Các đối tác MAX:** Unity Ads (32), Mintegral (702), Vungle (35).
- **DMA Signals:** Tích chọn "Google Ads User Data" và "Google Ads Personalization".

### 2.2. Facebook & Adjust Dashboard
- **Facebook:** Trong Events Manager, bật "Data Processing Options".
- **Adjust:** Bật hỗ trợ TCF v2.3 trong phần Privacy Settings để Adjust tự đọc TC String.

---

## 3. THIẾT LẬP NỀN TẢNG (PLATFORM CONFIG)

### Android (AndroidManifest.xml)
```xml
<!-- Advanced Consent Mode: defaults true, Firebase collects cookieless pings -->
<!-- setConsent() updates full consent state after user choice -->
<meta-data android:name="google_analytics_default_allow_analytics_storage" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_storage" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_user_data" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_personalization_signals" android:value="true" />

<!-- Facebook: disabled until consent -->
<meta-data android:name="com.facebook.sdk.AutoLogAppEventsEnabled" android:value="false" />
<meta-data android:name="com.facebook.sdk.AdvertiserIDCollectionEnabled" android:value="false" />
```

### iOS (Info.plist) - Auto-configured by ConsentBuildProcessor
```xml
<key>NSUserTrackingUsageDescription</key>
<string>This app uses your data to provide personalized ads and improve your experience.</string>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE</key>
<false/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE</key>
<false/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA</key>
<false/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS</key>
<false/>
```

---

## 4. CHI TIẾT CẬP NHẬT VENDOR CONSENT (SDK Implementation)

### 4.1. Firebase Consent Mode v2
**File:** `FirebaseModule.cs`

Firebase Unity SDK không expose `setConsent()` trực tiếp. SDK gọi qua native bridge:
- **Android:** `FirebaseAnalytics.getInstance().setConsent(EnumMap)` via JNI
- **iOS:** `[FIRAnalytics setConsent:@{...}]` via native plugin

Mapping:
```
ConsentStatus.CanStoreAdData        → AD_STORAGE
ConsentStatus.CanCollectAnalytics   → ANALYTICS_STORAGE
ConsentStatus.CanTrackAttribution   → AD_USER_DATA
ConsentStatus.CanShowPersonalizedAds → AD_PERSONALIZATION
```

### 4.2. Facebook SDK
**File:** `ConsentManager.cs` → `ApplyFacebookConsentInternal()`

```csharp
FB.Mobile.SetAutoLogAppEventsEnabled(status.CanCollectAnalytics);
FB.Mobile.SetAdvertiserIDCollectionEnabled(status.CanTrackAttribution);
FB.Mobile.SetDataProcessingOptions(
    new string[] { status.IsDoNotSell ? "LDU" : "" }, 0, 0);
// iOS only:
FB.Mobile.SetAdvertiserTrackingEnabled(status.HasAttConsent);
```

### 4.3. Meta (Facebook) qua AppLovin MAX
**File:** `AppLovinMaxProvider.cs`

```csharp
MaxSdk.SetHasUserConsent(consent.CanShowPersonalizedAds);
MaxSdk.SetDoNotSell(consent.IsDoNotSell);
MaxSdk.SetMetaData("facebook_limited_data_use",
    consent.CanShowPersonalizedAds ? "false" : "true");
```

### 4.4. Adjust Attribution
**File:** `AdjustTrackingProvider.cs`

Gọi `SendThirdPartySharing()` TRƯỚC `Adjust.InitSdk()`:
```
google_dma: eea, ad_personalization, ad_user_data, ad_storage, npa
facebook: data_processing_options_country, data_processing_options_state
```
Sau session start: `Adjust.TrackMeasurementConsent(consent.CanCollectAnalytics)`

---

## 5. BẢNG TRA CỨU VENDOR & PURPOSE ID

| Đối tác | Vendor ID | Purpose Quan trọng | Loại Consent |
|---------|-----------|-------------------|--------------|
| Google Ads | 755 | 1, 3, 4 | TCF + DMA Mode v2 |
| Meta (FB) | 31 | 1, 3, 4 | TCF + Manual LDU |
| AppLovin | 311 (AC) | 1, 2, 7 | Additional Consent |
| Unity Ads | 32 | 1, 2, 3 | TCF |
| Adjust | N/A | 7, 8, 9 | Manual Status |

---

## 6. SCRIPT HỖ TRỢ: ConsentHelper.cs

**File:** `com.archerstudio.sdk.consent/Runtime/ConsentHelper.cs`

```csharp
// Kiểm tra Purpose consent (1-based per IAB spec)
ConsentHelper.IsPurposeGranted(1)  // Purpose 1: Storage/access
ConsentHelper.IsPurposeGranted(3)  // Purpose 3: Personalized ads profile
ConsentHelper.IsPurposeGranted(7)  // Purpose 7: Measure ad performance

// Kiểm tra Vendor consent
ConsentHelper.IsVendorGranted(31)  // Meta (Facebook)
ConsentHelper.IsVendorGranted(755) // Google Ads

// Raw TCF data
ConsentHelper.GetTcString()        // Full TC String
ConsentHelper.IsGdprApplies()      // GDPR region?
```

---

## 7. MASTER CHECKLIST NGHIỆM THU

### GIAI ĐOẠN 1: CẤU HÌNH DASHBOARD
- [ ] AdMob UMP: Chuyển Ad Partners sang Custom Selection
- [ ] AdMob UMP: Thêm Meta (ID: 31), AppLovin (ID: 311), Google Ads (ID: 755)
- [ ] AdMob UMP: Bật DMA signals
- [ ] Facebook Events Manager: Bật Data Processing Options
- [ ] Adjust Dashboard: Bật TCF v2.3 support

### GIAI ĐOẠN 2: CẤU HÌNH PROJECT
- [ ] SDK Versions: UMP, MAX, FB, Adjust bản mới nhất 2026
- [ ] Android Manifest: 4 Google Analytics defaults configured
- [ ] iOS Info.plist: Auto-configured by ConsentBuildProcessor (consent defaults + ATT description)
- [ ] SKAdNetwork IDs: Cập nhật từ AppLovin và Facebook

### GIAI ĐOẠN 3: CODE VERIFICATION
- [ ] ConsentManager priority = 0 (init đầu tiên)
- [ ] Firebase Consent Mode v2: setConsent() called via native bridge
- [ ] Facebook: LDU + AutoLog + AdvertiserID based on consent
- [ ] MAX: SetHasUserConsent + SetDoNotSell + facebook_limited_data_use metadata
- [ ] Adjust: ThirdPartySharing with DMA params BEFORE InitSdk
- [ ] Adjust: TrackMeasurementConsent uses actual consent value
- [ ] MAX InitializeSdk() called last (priority 50)

### GIAI ĐOẠN 4: KIỂM TRA QA
- [ ] DebugGeography.EEA: Bảng UMP hiển thị đúng
- [ ] "Accept All": Tất cả SDK kích hoạt, Firebase logcat shows AD_STORAGE=granted
- [ ] "Do Not Consent": Limited Ads only, Firebase logcat shows AD_STORAGE=denied
- [ ] PlayerPrefs: IABTCF_tcString có dữ liệu sau consent
- [ ] Nút "Privacy Settings": Mở lại được bảng UMP
- [ ] Logcat Facebook: LDU/AdvertiserTrackingEnabled log
- [ ] Logcat Adjust: IAB TCF string found in local storage

### GIAI ĐOẠN 5: GAMEPLAY
- [ ] Rewarded Ads: Load được Limited Ads khi user từ chối consent
- [ ] Firebase Analytics: Events vẫn ghi nhận (cookieless) khi consent denied (Advanced mode)
