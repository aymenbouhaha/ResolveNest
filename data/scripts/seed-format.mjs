export function parseCsv(text) {
  const rows = [];
  let row = [];
  let field = "";
  let insideQuotes = false;
  for (let index = 0; index < text.length; index += 1) {
    const character = text[index];
    if (insideQuotes) {
      if (character === '"' && text[index + 1] === '"') {
        field += '"';
        index += 1;
      } else if (character === '"') {
        insideQuotes = false;
      } else {
        field += character;
      }
    } else if (character === '"') {
      insideQuotes = true;
    } else if (character === ",") {
      row.push(field);
      field = "";
    } else if (character === "\n" || character === "\r") {
      if (character === "\r" && text[index + 1] === "\n") index += 1;
      row.push(field);
      rows.push(row);
      row = [];
      field = "";
    } else {
      field += character;
    }
  }
  if (insideQuotes) throw new Error("Unterminated quoted field");
  if (field !== "" || row.length > 0) {
    row.push(field);
    rows.push(row);
  }
  const [header, ...dataRows] = rows;
  return dataRows.map((values, rowIndex) => {
    if (values.length !== header.length) {
      throw new Error(`Row ${rowIndex + 2} has ${values.length} fields, expected ${header.length}`);
    }
    return Object.fromEntries(header.map((column, columnIndex) => [column, values[columnIndex]]));
  });
}

export function formatCsvRow(values) {
  return values
    .map((value) => {
      const text = String(value ?? "");
      return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
    })
    .join(",");
}

export const normalizeCustomerRef = (value) => value.trim().toLowerCase();
export const normalizeBugRef = (value) => value.trim().toUpperCase();
export const normalizeFeatureRef = (value) => value.trim().toLowerCase();

export function normalizeVersion(rawVersion) {
  if (rawVersion === null || rawVersion === undefined) return { kind: "missing" };
  const trimmed = rawVersion.trim().replace(/^v/i, "");
  if (/^\d+\.\d+\.\d+$/.test(trimmed)) return { kind: "exact", version: trimmed };
  if (/^\d+\.\d+$/.test(trimmed)) return { kind: "minor-line", line: trimmed };
  return { kind: "unparseable", raw: rawVersion };
}

export function compareVersions(left, right) {
  const leftParts = left.split(".").map(Number);
  const rightParts = right.split(".").map(Number);
  for (let index = 0; index < 3; index += 1) {
    const difference = (leftParts[index] ?? 0) - (rightParts[index] ?? 0);
    if (difference !== 0) return Math.sign(difference);
  }
  return 0;
}
