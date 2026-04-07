#import <Foundation/Foundation.h>

#if __has_include(<FirebaseAnalytics/FIRAnalytics.h>)
#import <FirebaseAnalytics/FIRAnalytics.h>
#define HAS_FIREBASE_ANALYTICS 1
#else
#define HAS_FIREBASE_ANALYTICS 0
#endif

void ArcherSDK_SetFirebaseConsent(bool adStorage, bool analyticsStorage, bool adUserData, bool adPersonalization) {
#if HAS_FIREBASE_ANALYTICS
    [FIRAnalytics setConsent:@{
        FIRConsentTypeAdStorage : adStorage ? FIRConsentStatusGranted : FIRConsentStatusDenied,
        FIRConsentTypeAnalyticsStorage : analyticsStorage ? FIRConsentStatusGranted : FIRConsentStatusDenied,
        FIRConsentTypeAdUserData : adUserData ? FIRConsentStatusGranted : FIRConsentStatusDenied,
        FIRConsentTypeAdPersonalization : adPersonalization ? FIRConsentStatusGranted : FIRConsentStatusDenied,
    }];
#endif
}
