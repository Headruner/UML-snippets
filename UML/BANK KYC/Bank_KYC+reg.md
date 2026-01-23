# US#1 - Mobile registration of the new user
## As a New User, I want to register in mobile application so then I can use it for getting Bank services anywhere.
## Acceptance criteria
1. The application must be installed with all necessary permission for it requested.
2. Initial checks
    1. the application was re-installed and any local files should be ignored if last action on the server side was uninstall (to avoid fraud).
    2. minimum requirements are met (hardware and software) on the app launch.
    3. major update of the application is available on the app launch and block further actions until app is updated.
    4. necessary permissions are granted (internet, A-GPS, notifications) and prompt to provide such permissions if not enabled. Notifications are not strictly required, other requirements are.
    5. local files include valid access token and bypass registration steps in the such a case.
3. When launch checks pass and no active user is found, the system must prompt a registration screen.
4. The User must be able navigate between registration screen, see the current step of the progress and be able to update information before application.
5. The system must use 'secondary' communication channels to verify user's e-mail and phone number with one-time passwords.
    1. password TTL is 5 minutes
    2. password must be unique and hard to guess but easy to use (6 digits)
    3. the system must block further steps (KYC) before e-mail and phone number are verified
6. Embedded KYC web view
    1. The KYC provider’s URL must be loaded in an in‑app WebView configured with: 
    2. HTTPS with TLS 1.3 only
    3. The WebView must enforce certificate pinning to the KYC provider’s public key.
    4. The provider’s page must set X‑Frame‑Options: SAMEORIGIN to prevent click‑jacking.
    5. The KYC web view must load (i.e., receive the first HTML response) within 3 seconds on a 3G connection for 95 % of sessions under normal load.
    6. All KYC‑related API calls must be logged with a correlation ID that can be traced back to the stored session token for forensic review.
6. Session persistence
    1. Upon successful launch of the KYC web view, the server returns a KYC session token (UUID).
    2. The token must be stored encrypted in the Android EncryptedSharedPreferences (or iOS Keychain) under the key kyc_session_token.
    3. On app resume or re‑launch, the stored token must be read; if it is still valid (server confirms), the app must restore the UI to the last pending step.
    4. When the KYC flow finishes (success or failure), the token must be cleared from storage.
7. Security of data in transit
    1. No plain‑text logs or screenshots may capture the KYC payload; any debug output must be stripped of PII before being written.
    2. Privacy guarantee
    3. The app must never write scanned documents or personal data to the device’s external storage or cache directory.
    4. After a successful upload, the in‑memory buffer containing the document must be overwritten and the reference released.
8. Error handling & fallback
    1. If the WebView cannot be rendered (e.g., network unavailable, certificate error), display a retry dialog with an explanatory message and retain the stored session token.
    2. If the KYC provider returns an unrecoverable error, the app must navigate the user back to the registration wizard with a clear error description.


# US#2 - KYC invoke  process inside the mobile app
## As the banking platform, I need to embed the third‑party KYC verification flow inside the mobile app and persist the user’s progress so that the user can complete identity verification without leaving the app or re‑entering data.
## Acceptance criteria
1. The system must be able to invoke a KYC process as a third-party web-application inside the banking application for smooth experience.
2. The system must memorize current progress to support disconnected sessions (when user is distracted and returns back to the app later).
3. The system must provide a secured connection inside the app so then all data cannot be intercepted.
4. The system must process KYC results asynchronous and deliver e-mail/SMS to the user with results as a short link to continue registration or use of the application.
5. Upon successful launch of the KYC web view, the server returns a KYC session token (UUID).
6. The token must be stored encrypted in the Android EncryptedSharedPreferences (or iOS Keychain) under the key kyc_session_token.
7. On app resume or re‑launch, the stored token must be read; if it is still valid (server confirms), the app must restore the UI to the last pending step.
8. When the KYC flow finishes (success or failure), the token must be cleared from storage.
9. All HTTP/HTTPS traffic between the app and the KYC provider must use TLS 1.3. No plain‑text logs or screenshots may capture the KYC payload; any debug output must be stripped of PII before being written.
10. The app must never write scanned documents or personal data to the device’s external storage or cache directory.
11. After a successful upload, the in‑memory buffer containing the document must be overwritten and the reference released.
12. If the WebView cannot be rendered (e.g., network unavailable, certificate error), display a retry dialog with an explanatory message and retain the stored session token.
13. If the KYC provider returns an unrecoverable error, the app must navigate the user back to the registration wizard with a clear error description.
14. The KYC web view must load (i.e., receive the first HTML response) within 3 seconds on a 3G connection for 95 % of sessions under normal load.
Auditability
15. ll KYC‑related API calls must be logged with a correlation ID that can be traced back to the stored session token for forensic review.


#UseCase #1 - Mobile app registration (no KYC)

| **Given**                                                                 | **When**                                                                                     | **Then**                                                                                     |
|---------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| User opens the app and is directed to registration (no session).         | System loads an empty registration form with fields: Name, Email, Phone.                    | Form renders with validation rules (e.g., email format, phone length).                        |
|                                                                           | User starts typing in a required field (e.g., Name).                                        | Field validates input in real-time (e.g., letters only for Name).                              |
|                                                                           | User submits the form with **valid** data.                                                  | System sends data to backend; proceeds to OTP verification or next step.                   |
|                                                                           | **Error**: Empty required field (e.g., Email).                                               | Form highlights the empty field and shows *"This is required."*                              |
|                                                                           | **Error**: Invalid email format (e.g., `userexample.com`).                                   | Field turns red; error tooltip: *"Please enter a valid email (user@example.com)."*         |
| User submits the form with **invalid** phone number (e.g., 9 digits).     | System validates phone length against rules (≥10 digits).                                     | Error displayed under field: *"Phone must be 10 digits."*                                    |
|                                                                           | User ignores validation errors and submits again.                                           | Same errors persist; no progress to next step.                                              |
| **Alternative**: User accidentally closes the app mid-registration.       | System detects unsaved data (if cached locally).                                            | On reopening, pre-fill form with saved inputs (if available).                               |
| **Error**: Device has **no internet connection**.                          | User attempts to submit the form offline.                                                  | Local cache stores inputs; sync button appears for later submission.                        |
|                                                                           | **Backend error** (e.g., server timeout) after submission.                                  | Show: *"Failed to save data. Please try again."* + Retry button.                           |