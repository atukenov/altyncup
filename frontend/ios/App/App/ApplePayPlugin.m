#import <Capacitor/Capacitor.h>

// Classic Objective-C runtime registration — the Swift-only CAPBridgedPlugin
// conformance in ApplePayPlugin.swift wasn't reliably discovered for a plugin
// living directly in the app target (several unresolved reports of this exact
// "UNIMPLEMENTED" symptom); this macro-based registration is the long-stable
// mechanism Capacitor's bridge has used across versions.
CAP_PLUGIN(ApplePayPlugin, "ApplePay",
  CAP_PLUGIN_METHOD(canMakePayments, CAPPluginReturnPromise);
  CAP_PLUGIN_METHOD(startPayment, CAPPluginReturnPromise);
)
