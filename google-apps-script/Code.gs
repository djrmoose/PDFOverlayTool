/**
 * Overlay Compare Tool — beta telemetry receiver.
 *
 * SETUP (one time):
 * 1. Create a Google Sheet with headers in row 1 (see SHEET_HEADERS below).
 * 2. Extensions → Apps Script → paste this file → Save.
 * 3. Deploy → New deployment → Web app
 *      Execute as: Me
 *      Who has access: Anyone (even anonymous)   ← required for desktop app POSTs
 * 4. Authorize: open the /exec URL in a browser once and approve access.
 * 5. Copy the /exec URL into either:
 *      %LocalAppData%\PdfOverlayTool\telemetry.json  (recommended — see telemetry.json.example)
 *    or TelemetryConfig.WebAppUrl in the C# project.
 *
 * TROUBLESHOOTING "Access Denied":
 * - Must be "Anyone (even anonymous)", not "Anyone with Google account".
 * - Use the URL from the latest deployment (/exec), not /dev or an old deployment.
 * - Run doGet once in a browser to authorize the script first.
 * - Test GET: paste the /exec URL in Chrome — you should see JSON, not "Access Denied".
 *
 * SHEET_HEADERS (row 1):
 * Timestamp | Type | Name | Email | InstallId | Version | OS | IsAutoMode | SessionSeconds | Details
 */

var SHEET_HEADERS = [
  'Timestamp',
  'Type',
  'Name',
  'Email',
  'InstallId',
  'Version',
  'OS',
  'IsAutoMode',
  'SessionSeconds',
  'Details'
];

function doGet() {
  return jsonResponse_({
    ok: true,
    message: 'Telemetry endpoint is live. POST JSON from the Overlay Compare Tool app.'
  });
}

function doPost(e) {
  try {
    var sheet = SpreadsheetApp.getActiveSpreadsheet().getActiveSheet();
    ensureHeaders_(sheet);

    var data = JSON.parse(e.postData.contents);

    sheet.appendRow([
      new Date(),
      data.type || '',
      data.name || '',
      data.email || '',
      data.installId || '',
      data.version || '',
      data.os || '',
      data.isAutoMode === true || data.isAutoMode === 'true' ? 'true' : 'false',
      data.sessionSeconds != null ? data.sessionSeconds : '',
      JSON.stringify(data.details || {})
    ]);

    return jsonResponse_({ ok: true });
  } catch (err) {
    return jsonResponse_({ ok: false, error: err.message });
  }
}

function ensureHeaders_(sheet) {
  if (sheet.getLastRow() > 0) {
    return;
  }

  sheet.appendRow(SHEET_HEADERS);
}

function jsonResponse_(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
