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

1.  Open your app's [info.plist](https://developer.apple.com/documentation/bundleresources/information_property_list) file.
2.  Add the consent mode key-value pairs. The key describes the [consent type](about:/tag-platform/security/concepts/consent-mode#consent-types) and the value indicates consent state. Values can either be `true`, meaning consent was granted, or `false`, meaning consent was denied.

    In accordance with the updates to consent mode for traffic in European Economic Area (EEA), a value of `eu_consent_policy` can be set for `ad_user_data` and `ad_personalization`, meaning consent is denied only for users in regions subject to the EU User Consent Policy.

    Set the following:

    *   `GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE`
    *   `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE`
    *   `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA`
    *   `GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS`
3.  Save your changes. Next, implement the mechanism to update consent values.


For example, to set all grant consent for all parameters by default:

```
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_ANALYTICS_STORAGE</key> <true/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_STORAGE</key> <true/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA</key> <true/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS</key> <true/>

```


### Update consent

To update consent values after an app has launched, call the [`setConsent`](https://firebase.google.com/docs/reference/android/com/google/firebase/analytics/FirebaseAnalytics#setConsent) method.

The value set by the `setConsent` method overrides the default setting and persists across app executions. The value remains in that state until `setConsent` is called again, even if a user closes and reopens the app. `setConsent` only updates the parameters you specify.

> If a user withdraws their previously given consent for Analytics or Ad storage, Google Analytics deletes all user properties, including consent for [`ad_personalization`](https://developers.google.com/tag-platform/security/concepts/consent-mode#consent-type). To preserve the user's consent choice for ad personalization, restore the previous value for ad personalization using `setConsent` ([Swift](https://firebase.google.com/docs/reference/swift/firebaseanalytics/api/reference/Categories/FIRAnalytics\(Consent\)#setconsent_:) | [Obj-C](https://firebase.google.com/docs/reference/ios/firebaseanalytics/api/reference/Classes/FIRAnalytics#+setconsent:)) .

The following example shows the `setConsent` method updating the different consent values to `granted`:

### Swift

```
Analytics.setConsent([
  .analyticsStorage: .granted,
  .adStorage: .granted,
  .adUserData: .granted,
  .adPersonalization: .granted,
])

```


### Objective-C

```
[FIRAnalytics setConsent:@{
  FIRConsentTypeAnalyticsStorage : FIRConsentStatusGranted,
  FIRConsentTypeAdStorage : FIRConsentStatusGranted,
  FIRConsentTypeAdUserData : FIRConsentStatusGranted,
  FIRConsentTypeAdPersonalization : FIRConsentStatusGranted,
}];

```


If a user decides to revoke their consent, make sure you update the consent states accordingly

Upgrade to consent mode v2
--------------------------

As a part of Google's ongoing commitment to a privacy-safe digital advertising ecosystem, we are strengthening the enforcement of our [EU user consent policy](https://www.google.com/about/company/user-consent-policy/).

Learn more about Google's [Updates to consent mode for traffic in European Economic Area (EEA)](https://support.google.com/tagmanager/answer/13695607).

Consent mode users need to send two new parameters in addition to ad storage and analytics storage:

1.  Update your app's info.plist to include:

    ```
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_USER_DATA</key> <true/>
<key>GOOGLE_ANALYTICS_DEFAULT_ALLOW_AD_PERSONALIZATION_SIGNALS</key> <true/>

```

    
2.  Update your consent calls to include the parameters for ad user data and ad personalization:
    
    ### Swift
    
    ```
Analytics.setConsent([
.analyticsStorage: .granted,
.adStorage: .granted,
.adUserData: .granted,
.adPersonalization: .granted,
])

```


    ### Objective-C
    
    ```
[FIRAnalytics setConsent:@{
FIRConsentTypeAnalyticsStorage : FIRConsentStatusGranted,
FIRConsentTypeAdStorage : FIRConsentStatusGranted,
FIRConsentTypeAdUserData : FIRConsentStatusGranted,
FIRConsentTypeAdPersonalization : FIRConsentStatusGranted,
}];

```

    

Verify consent settings
-----------------------

You can verify that your consent settings are working as intended by viewing the Xcode debug console for your app.

Follow these steps:

1.  [Enable verbose logging](https://firebase.google.com/docs/analytics/events?platform=ios#view_events_in_the_xcode_debug_console) on your device.
2.  In the Xcode debug console, look for:
    
    *   `ad_storage`
    *   `analytics_storage`
    *   `ad_user_data`
    *   `ad_personalization`
    
    For example, if Ad storage are enabled, you'll see the following message:
    
    ```
ad_storage is granted.

```
