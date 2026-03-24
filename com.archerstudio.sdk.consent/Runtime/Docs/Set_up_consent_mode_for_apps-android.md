# Set up consent mode for apps  |  Tag Platform  |  Google for Developers
*   This guide helps Firebase developers integrate consent mode to control data collection based on user consent, covering both basic and advanced implementation options.

*   Before setup, ensure you've implemented the Google Analytics for Firebase SDK and a consent settings banner or CMP.

*   To set up consent mode, define default consent states in your app's info.plist file and use the `setConsent` method to dynamically update consent values based on user choices.

*   Consent mode v2 introduces two additional parameters, `ad_user_data` and `ad_personalization`, to comply with Google's updated EU user consent policy, requiring updates to your app's info.plist and consent calls.

*   Verify your implementation by checking the Xcode debug console for consent settings like `ad_storage`, `analytics_storage`, `ad_user_data`, and `ad_personalization`.


> This page is for developers that use the Google Analytics for Firebase SDK in their app and want to integrate consent mode. For an introduction to consent mode, read [Consent mode overview](https://developers.google.com/tag-platform/security/concepts/consent-mode).

Google Analytics offers consent mode to adjust how your SDK behaves based on the consent status of your users. You can implement consent mode in a basic or advanced way. If you aren't sure whether to implement basic or advanced consent mode, learn more about [basic versus advanced consent mode](about:/tag-platform/security/concepts/consent-mode#basic-vs-advanced) and check with your company's guidelines.

Before you begin
----------------

Before you can manage user consent, you need to implement:

*   [Google Analytics for Firebase SDK](https://firebase.google.com/docs/analytics)
*   A consent settings banner to capture user consent


To set up consent mode, you need to:

1.  [Set the default consent state](#default-consent).

### Set the default consent state

By default, no consent mode values are set. To set the default consent state for your app:

1.  Open your app's [AndroidManifest.xml](https://developer.android.com/guide/topics/manifest/manifest-intro) file.
2.  Add the consent mode key-value pairs. The key describes the [consent type](about:/tag-platform/security/concepts/consent-mode#consent-types) and the value indicates consent state. Values can either be `true`, meaning consent was granted, or `false`, meaning consent was denied.

    In accordance with the updates to consent mode for traffic in European Economic Area (EEA), a value of `eu_consent_policy` can be set for `ad_user_data` and `ad_personalization`, meaning consent is denied only for users in regions subject to the EU User Consent Policy.

    Set the following:

    *   `google_analytics_default_allow_analytics_storage`
    *   `google_analytics_default_allow_ad_storage`
    *   `google_analytics_default_allow_ad_user_data`
    *   `google_analytics_default_allow_ad_personalization_signals`
3.  Save your changes. Next, implement the mechanism to update consent values.


For example, to set all grant consent for all parameters by default:

```
<meta-data android:name="google_analytics_default_allow_analytics_storage" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_storage" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_user_data" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_personalization_signals" android:value="true" />


```


### Update consent

To update consent values after an app has launched, call the [`setConsent`](https://firebase.google.com/docs/reference/android/com/google/firebase/analytics/FirebaseAnalytics#setConsent) method.

The value set by the `setConsent` method overrides the default setting and persists across app executions. The value remains in that state until `setConsent` is called again, even if a user closes and reopens the app. `setConsent` only updates the parameters you specify.

> If a user withdraws their previously given consent for Analytics or Ad storage, Google Analytics deletes all user properties, including consent for [`ad_personalization`](https://developers.google.com/tag-platform/security/concepts/consent-mode#consent-type). To preserve the user's consent choice for ad personalization, restore the previous value for ad personalization using `setConsent` ([Kotlin+KTX](https://firebase.google.com/docs/reference/kotlin/com/google/firebase/analytics/FirebaseAnalytics#setConsent\(kotlin.collections.MutableMap\)) | [Java](https://firebase.google.com/docs/reference/android/com/google/firebase/analytics/FirebaseAnalytics#setConsent)) .

The following example shows the `setConsent` method updating the different consent values to `granted`:

### Java

```
// Set consent types.
Map<ConsentType, ConsentStatus> consentMap = new EnumMap<>(ConsentType.class);
consentMap.put(ConsentType.ANALYTICS_STORAGE, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_STORAGE, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_USER_DATA, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_PERSONALIZATION, ConsentStatus.GRANTED);

mFirebaseAnalytics.setConsent(consentMap);

```


### Kotlin

```
Firebase.analytics.setConsent {
  analyticsStorage(ConsentStatus.GRANTED)
  adStorage(ConsentStatus.GRANTED)
  adUserData(ConsentStatus.GRANTED)
  adPersonalization(ConsentStatus.GRANTED)
}

```


If a user decides to revoke their consent, make sure you update the consent states accordingly

Upgrade to consent mode v2
--------------------------

As a part of Google's ongoing commitment to a privacy-safe digital advertising ecosystem, we are strengthening the enforcement of our [EU user consent policy](https://www.google.com/about/company/user-consent-policy/).

Learn more about Google's [Updates to consent mode for traffic in European Economic Area (EEA)](https://support.google.com/tagmanager/answer/13695607).

Consent mode users need to send two new parameters in addition to ad storage and analytics storage:

1.  Update your app's AndroidManifest.xml to include:

    ```
<meta-data android:name="google_analytics_default_allow_ad_user_data" android:value="true" />
<meta-data android:name="google_analytics_default_allow_ad_personalization_signals" android:value="true" />
```

```

    
2.  Update your consent calls to include the parameters for ad user data and ad personalization:
    
    ### Java
    
    ```
// Set consent types.
Map<ConsentType, ConsentStatus> consentMap = new EnumMap<>(ConsentType.class);
consentMap.put(ConsentType.ANALYTICS_STORAGE, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_STORAGE, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_USER_DATA, ConsentStatus.GRANTED);
consentMap.put(ConsentType.AD_PERSONALIZATION, ConsentStatus.GRANTED);

mFirebaseAnalytics.setConsent(consentMap);

```


    ### Kotlin
    
    ```
Firebase.analytics.setConsent {
analyticsStorage(ConsentStatus.GRANTED)
adStorage(ConsentStatus.GRANTED)
adUserData(ConsentStatus.GRANTED)
adPersonalization(ConsentStatus.GRANTED)
}

```

    

Verify consent settings
-----------------------

You can verify that your consent settings are working as intended by viewing the log messages for your app.

Follow these steps:

1.  [Enable verbose logging](https://firebase.google.com/docs/analytics/events?platform=android#view_events_in_the_android_studio_debug_log) on your device.
2.  In the Android Studio logcat, find the log message that starts with `Setting consent`. For example, Ad storage is enabled, you'll see the following log message:
    
    ```
Setting consent, ... AD_STORAGE=granted

```
