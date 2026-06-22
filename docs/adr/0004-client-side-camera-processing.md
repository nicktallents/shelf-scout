# Barcode scanning and expiration date OCR run entirely client-side

Barcode decoding (zxing-js / BarcodeDetector API) and expiration date OCR (Tesseract.js) execute in the browser. The backend is not involved. Product name lookup uses the Open Food Facts REST API directly from the client.

This was a deliberate constraint: the app must have no recurring infrastructure cost beyond the domain. Server-side OCR or a paid vision API (e.g., Google Vision, Claude) would introduce per-request charges. Running both libraries in-browser satisfies the constraint at the cost of slightly higher initial page load and processing that is bounded by the device's CPU rather than server resources.

## Consequences

AI-based food identification (for items with no barcode) is deferred to a future release, as it cannot be done client-side without an API call to a paid vision model.
