# AI-First Budget Tracker – MVP & Growth Plan

## 1. End-to-End User Flows (MVP → v1)

### A. First-Run Onboarding (5 mins)
1. Create account → set currency, country, time zone.  
2. Pick starter categories (preloaded) + optional custom ones.  
3. Import wizard:  
   - Drop CSV/PDF statements (multiple accounts allowed).  
   - Show live “parse preview” with column mapping and a few sample rows.  
   - User confirms/edits mappings; save as a **bank template** for next time.  
4. (Optional) Quick goals: “Spend ≤ $400 dining this month,” “Save $2k in 6 months.”  
5. Finish → first dashboard + “Start a chat” CTA.

### B. Statement Import & Review
1. User uploads a file.  
2. Pipeline:  
   - **Extractor** (CSV: dialect sniff + header inference; PDF: text + OCR fallback)  
   - **Normalizer** (date formats, signs, amounts, merchant cleaning)  
   - **Deduper** (hash across {amount, normalized_merchant, posted_date±3, account_id})  
   - **Categorizer** (rules → ML → LLM fallback)  
3. Review screen: “X new, Y duplicates skipped, Z flagged (splits/transfers).”  
4. One-click fixes: “Mark as transfer,” “Split,” “Reassign category,” “Create rule from this.”

### C. Home Dashboard (MVP)
- This month: Income, Expenses, Net, Top categories, Burn rate, Upcoming recurring.  
- Smart tiles:  
  - “You’re 72% through your Dining budget with 11 days left.”  
  - “Unusual: $189 at ABC Electronics; 12-month avg for this merchant is $0.”  
- CTA: **Ask me anything** (opens chat).

### D. Conversational Analytics (Differentiator)
Natural questions → answers + charts:  
- “How much did I spend on groceries in August?”  
- “Compare last 3 months dining vs groceries.”  
- “What subscriptions renew next month?”  
- “If I cut rideshare 30%, when do I hit $5k savings?” (counterfactuals)  
- “Explain this $189 charge” → merchant enrichment, past history, receipt if attached.

### E. Budgets & Goals
- Wizard suggests budgets from last 3 months medians.  
- Goals: amount + target date + category constraints.  
- Nudges via notifications: “You’re trending +22% vs typical mid-month spend.”

### F. Month-End Close
- Recap: trendlines, biggest movers, anomalies, subscription changes, goal progress.  
- “What changed?” narrative auto-generated.  
- Export CSV/PDF.

---

## 2. AI/LLM Differentiators (beyond Mint-style)
1. **Conversational Finance Coach** – monthly/weekly narratives (“why” not just “what”).  
2. **Anomaly & “What Changed?” Explanations** – detect spikes or new categories with context.  
3. **Semantic Merchant Enrichment** – normalize merchant strings like `AMZN Mktp` → “Amazon – Online shopping”.  
4. **Recurring Detection + Subscription Hygiene** – trials, price hikes, dormant subs.  
5. **Counterfactual “What-Ifs” & Micro-Challenges** – simulate cutbacks, savings impact.  
6. **Receipt AI (optional)** – snap receipts → itemize & split line items.  
7. **Cross-currency & UPI awareness (future)** – handle INR/USD, FX conversions.

---

## 3. Data Model (simplified)
- **accounts** – id, user_id, name, type, currency.  
- **transactions** – id, user_id, account_id, txn_date, amount, merchant, category_id, flags, metadata.  
- **merchants** – id, display_name, aka, embedding_vec.  
- **categories** – id, parent_id, name, budget.  
- **recurring_series** – merchant_id, cadence, avg_amount.  
- **rules** – pattern → actions.  
- **files_imported** – id, user_id, filename, stats.  
- **goals** – target_amount, by_date, scope.  
- **events_audit** – event logs.

---

## 4. Ingestion & Categorization Pipeline
- **CSV** – dialect sniff, header mapping, normalize dates.  
- **PDF** – extract text, OCR fallback, user-saved templates.  
- **Dedup** – fingerprint = SHA256(amount|merchant|date).  
- **Categorization** – rules → embeddings → LLM fallback.  
- **Transfers** – detect equal/opposite txns.  
- **Splits** – manual or receipt AI.

---

## 5. Conversational Analytics Architecture
- **Tools/Functions** – `list_metrics()`, `run_metric()`, `get_recurring()`, `what_if()`.  
- **DSL layer** – constrained queries compiled to SQL.  
- **Guardrails** – read-only, user_id filter, limits.  
- **Formatter** – text + chart; option to pin insight.  
- **Example**: “How much on groceries Aug 2025?” → DSL → SQL → chart.
- **Example**: "I spend 50$ at McDonalds today"

---

## 6. MVP Feature Checklist
**Weeks 1–4**  
- Auth, accounts, categories.  
- CSV import + rules engine.  
- Dashboard v0.  
- Chat v0 (simple queries).  

**Weeks 5–8**  
- PDF templates, embeddings, recurring, recap.  
- Chat v1 (comparisons, explanations).  
- Budgets & goals.  

**Weeks 9–12**  
- Splits, transfers, refunds.  
- Multi-currency, exports.  
- Counterfactuals, anomaly detection.  
- Pro paywall + referrals.

---

## 7. Tech Stack
- **Frontend** –nuxt/vue, Recharts.  
- **Backend** – C#, .net core, PostgreSQL (+pgvector), Azure Blob.  
- **AI** – OpenAI / local LLMs, embeddings, toolformer-style prompts.  
- **Analytics** – dbt, Metabase. 
- **Database** - PostgreSQL (+pgvector), Vector Search
- **Worker** - C# .net core
- **Cloud** - Azure

---

## 8. Security & Privacy
- Encrypt at rest + transit.  
- Row-level security by user_id.  
- Short-lived signed URLs.  
- Hard delete option.  
- Secret mgmt (Vault/SSM).  
- Audit logging.  

---

## 9. Monetization
- **Freemium** – imports, rules, budgets, basic chat.  
- **Pro ($4–7/mo)** – advanced AI chat, anomalies, subscriptions, multi-currency, receipt AI, family budgets.  
- **Affiliate** – suggest better cards/accounts → referral.  
- **Growth loops** – shareable recap cards, referrals, community templates.

---

## 10. Execution Roadmap (90 days)
- **Day 0–30** – CSV import, dashboard v0, chat v0, budgets.  
- **Day 31–60** – PDF templates, merchant embeddings, recurring/subscriptions.  
- **Day 61–90** – What-ifs, anomalies, Pro plan, referrals.

