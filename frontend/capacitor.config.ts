import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yurt.app',
  appName: 'Altyncup',
  webDir: 'dist/yurt-customer/browser',
  plugins: {
    // Firebase Phone Auth sends the 6-digit SMS when the backend runs with Otp__Provider=Firebase.
    FirebaseAuthentication: {
      skipNativeAuth: false,
      providers: ['phone'],
    },
  },
};

export default config;
