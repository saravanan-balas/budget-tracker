#!/usr/bin/env node
/**
 * Generates MerchantCategoryKeywords.json from the greggles/mcc-codes dataset.
 * Run: node scripts/generate-mcc-keywords.js
 * 
 * Fetches mcc_codes.csv from GitHub and produces category→keywords mapping
 * for all MCC entries (not limited to previously hardcoded values).
 */

const fs = require('fs');
const path = require('path');
const https = require('https');

const MCC_CSV_URL = 'https://raw.githubusercontent.com/greggles/mcc-codes/main/mcc_codes.csv';
const OUTPUT_PATH = path.join(__dirname, '../common/BudgetTracker.Common/Data/MerchantCategoryKeywords.json');

// Map MCC irs_description to app category names. Unmapped use first part of irs_description.
const IRS_TO_APP_CATEGORY = {
  'Airlines': 'Transportation',
  'Car Rental': 'Transportation',
  'Hotels/Motels/Inns/Resorts': 'Travel',
  'Veterinary Services': 'Pet Care',
  'Grocery Stores': 'Groceries',
  'Supermarkets': 'Groceries',
  'Eating Places': 'Dining Out',
  'Restaurants': 'Dining Out',
  'Fast Food Restaurants': 'Dining Out',
  'Eating Places, Restaurants': 'Dining Out',
  'Drug Stores': 'Healthcare',
  'Drug Stores and Pharmacies': 'Healthcare',
  'Service Stations': 'Gas',
  'Service Stations (With or Without Ancillary Services)': 'Gas',
  'Lodging - Hotels, Motels, Resorts, Central Reservation Services': 'Travel',
  'Heating, Plumbing, A/C': 'Utilities',
  'General Contractors': 'Rent/Mortgage',
};

function parseCSV(text) {
  const rows = [];
  let row = [];
  let cell = '';
  let inQuotes = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"') {
      inQuotes = !inQuotes;
    } else if (inQuotes) {
      cell += c;
    } else if (c === ',') {
      row.push(cell.trim());
      cell = '';
    } else if (c === '\n' || c === '\r') {
      if (c === '\n' || (c === '\r' && text[i + 1] !== '\n')) {
        row.push(cell.trim());
        if (row.some(x => x)) rows.push(row);
        row = [];
        cell = '';
      }
    } else {
      cell += c;
    }
  }
  if (cell || row.length) {
    row.push(cell.trim());
    if (row.some(x => x)) rows.push(row);
  }
  return rows;
}

const GENERIC_SKIP = new Set([
  'the', 'and', 'or', 'with', 'for', 'inc', 'inc.', 'see', 'not', 'elsewhere', 'classified',
  'services', 'other', 'general', 'miscellaneous', 'related', 'sales', 'service', 'work',
  'contractors', 'contractor', 'operatives', 'commercial', 'residential', 'installation'
]);

function extractKeywords(editedDesc) {
  if (!editedDesc || editedDesc.length < 2) return [];
  const normalized = editedDesc
    .replace(/[()\/,–—-]/g, ' ')
    .replace(/\s+/g, ' ')
    .toLowerCase()
    .trim();
  const words = normalized.split(/\s+/).filter(w => {
    if (w.length < 3) return false;
    if (/^\d+$/.test(w)) return false;
    if (GENERIC_SKIP.has(w)) return false;
    return true;
  });
  const keywords = new Set();
  words.forEach(w => keywords.add(w));
  if (normalized.length <= 60) {
    const phrase = normalized.replace(/\s+/g, ' ').replace(/\b(the|and|or|with|for)\b/g, '').replace(/\s+/g, ' ').trim();
    if (phrase.length >= 4) keywords.add(phrase);
  }
  return [...keywords];
}

function resolveCategory(irsDesc) {
  if (!irsDesc) return 'Other';
  const keys = Object.keys(IRS_TO_APP_CATEGORY).sort((a, b) => b.length - a.length);
  const key = keys.find(k => irsDesc === k || irsDesc.startsWith(k) || irsDesc.includes(k.split(',')[0]));
  return key ? IRS_TO_APP_CATEGORY[key] : irsDesc.split(',')[0].trim() || 'Other';
}

async function fetchCSV() {
  return new Promise((resolve, reject) => {
    https.get(MCC_CSV_URL, res => {
      if (res.statusCode !== 200) {
        reject(new Error(`Failed to fetch: ${res.statusCode}`));
        return;
      }
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => resolve(data));
    }).on('error', reject);
  });
}

async function main() {
  console.log('Fetching MCC codes from greggles/mcc-codes...');
  const csvText = await fetchCSV();

  const rows = parseCSV(csvText);
  const header = rows[0];
  const mccIndex = header.indexOf('mcc');
  const editedIndex = header.indexOf('edited_description');
  const irsIndex = header.indexOf('irs_description');

  if (mccIndex < 0 || editedIndex < 0 || irsIndex < 0) {
    throw new Error('Could not find required columns in MCC CSV');
  }

  const categoryToKeywords = new Map();

  for (let i = 1; i < rows.length; i++) {
    const row = rows[i];
    if (row.length <= Math.max(mccIndex, editedIndex, irsIndex)) continue;

    const editedDesc = row[editedIndex]?.trim();
    const irsDesc = row[irsIndex]?.trim();
    if (!editedDesc && !irsDesc) continue;

    const category = resolveCategory(irsDesc);
    const keywords = extractKeywords(editedDesc || irsDesc);

    if (!categoryToKeywords.has(category)) {
      categoryToKeywords.set(category, new Set());
    }
    keywords.forEach(kw => categoryToKeywords.get(category).add(kw));
  }

  const output = {};
  for (const [cat, kwSet] of categoryToKeywords.entries()) {
    const list = [...kwSet].filter(k => k.length >= 2).sort();
    if (list.length > 0) {
      output[cat] = list;
    }
  }

  const outDir = path.dirname(OUTPUT_PATH);
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  fs.writeFileSync(OUTPUT_PATH, JSON.stringify(output, null, 2), 'utf8');
  console.log(`Wrote ${Object.keys(output).length} categories with keywords to ${OUTPUT_PATH}`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
