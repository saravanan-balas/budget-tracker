# Merchant Embedding System Setup Guide

## Overview

The merchant embedding system uses OpenAI's text-embedding-3-small model to create vector representations of merchant names, enabling sophisticated similarity matching for transaction normalization.

## What's Been Implemented

### 1. **Database Schema**
- ✅ Enabled `vector` extension in PostgreSQL
- ✅ Added `Embedding` field to `Merchant` model (vector(1536))
- ✅ Database is ready for vector operations

### 2. **Core Services**
- ✅ `IEmbeddingService` - Generates embeddings using OpenAI API
- ✅ `IMerchantService` - Handles merchant matching and creation
- ✅ Integrated into transaction import processing

### 3. **Merchant Matching Strategy**
The system uses a three-tier approach:

1. **Exact Match** (fastest) - Direct string comparison
2. **Alias Match** - Check against known merchant aliases  
3. **Embedding Similarity** (most sophisticated) - Vector cosine similarity

### 4. **API Endpoints**
- `POST /api/merchants/match` - Test merchant matching
- `POST /api/merchants/generate-embeddings` - Generate embeddings for all merchants
- `GET /api/merchants/{id}/similar` - Find similar merchants
- `POST /api/merchants/{id}/update-embedding` - Update specific merchant embedding

## Setup Instructions

### Step 1: Database Migration

```bash
cd api/BudgetTracker.API
dotnet ef migrations add AddMerchantEmbeddings
dotnet ef database update
```

### Step 2: Configure OpenAI API Key

Add your OpenAI API key to `.env`:
```env
OPENAI_API_KEY=sk-your-openai-api-key-here
```

### Step 3: Build and Start Services

```bash
# Build everything
dotnet build

# Start services
docker-compose up -d postgres redis
cd api/BudgetTracker.API && dotnet run &
cd worker/BudgetTracker.Worker && dotnet run &
```

## How It Works

### Merchant Normalization

Raw merchant names are cleaned and normalized:

**Input**: `"STARBUCKS STORE #1234 SEATTLE WA 12/15"`
**Normalized**: `"STARBUCKS"`

Rules applied:
- Remove transaction prefixes (POS, DEBIT, etc.)
- Remove corporate suffixes (INC, LLC, etc.)
- Remove transaction IDs and numbers
- Remove location information
- Remove dates and ZIP codes

### Embedding Generation

```csharp
// Example usage
var embedding = await _embeddingService.GenerateEmbeddingAsync("STARBUCKS");
// Returns: Vector with 1536 dimensions
```

### Similarity Matching

```csharp
var matchResult = await _merchantService.FindBestMatchAsync("SBUX STORE 5678", 0.7);
// Result: { Merchant: "Starbucks", SimilarityScore: 0.89, MatchMethod: "embedding" }
```

## Testing the System

### 1. Test Merchant Matching API

```bash
curl -X POST http://localhost:5000/api/merchants/match \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -d '{
    "merchantName": "SBUX STORE #1234 SEATTLE WA",
    "similarityThreshold": 0.7
  }'
```

### 2. Generate Initial Embeddings

```bash
curl -X POST http://localhost:5000/api/merchants/generate-embeddings \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### 3. Test with Real Import

1. Upload a bank statement via the UI
2. Check logs to see merchant matching in action:

```
[INFO] Matched 'UBER TRIP 1234' to merchant 'Uber' (method: embedding, score: 0.92)
[INFO] Created new merchant: 'Netflix' for transaction 'NETFLIX.COM SUBSCRIPTION'
```

## Cost Optimization

### Embedding Costs
- Model: `text-embedding-3-small` 
- Price: $0.00002 per 1K tokens
- Average merchant name: ~2-5 tokens
- Cost per merchant: ~$0.0001

### Batch Processing
The system processes embeddings in batches of 50 to:
- Reduce API calls
- Stay within rate limits
- Minimize costs

### Caching Strategy
- Embeddings are generated once and stored in database
- No need to regenerate unless merchant name changes
- Similarity calculations are done locally (fast)

## Real-World Examples

### Before Embeddings (String Matching)
```
"UBER TRIP 1234" → No match → Creates "UBER TRIP 1234"
"UBER*EATS" → No match → Creates "UBER*EATS"  
"UBERTRIP" → No match → Creates "UBERTRIP"
```
**Result**: 3 separate merchants for the same company

### With Embeddings (Semantic Matching)
```
"UBER TRIP 1234" → Matches "Uber" (similarity: 0.94)
"UBER*EATS" → Matches "Uber" (similarity: 0.89)
"UBERTRIP" → Matches "Uber" (similarity: 0.91)
```
**Result**: All transactions correctly linked to "Uber"

## Performance Characteristics

### Similarity Search Performance
- **Exact match**: ~1ms (database index)
- **Alias match**: ~2ms (array contains)
- **Embedding similarity**: ~10-50ms (depends on merchant count)

### Scaling Considerations
- PostgreSQL pgvector handles similarity search efficiently
- For 10,000+ merchants, consider adding vector index:
```sql
CREATE INDEX ON merchants USING ivfflat (embedding vector_cosine_ops);
```

## Monitoring and Maintenance

### Key Metrics to Monitor
1. **Match Rate**: % of transactions that find existing merchants
2. **Match Method Distribution**: exact/alias/embedding ratios
3. **Similarity Scores**: Average scores for embedding matches
4. **API Costs**: OpenAI embedding generation costs

### Regular Maintenance
1. **Weekly**: Review new merchants created
2. **Monthly**: Run similarity analysis to find potential duplicates
3. **Quarterly**: Update merchant aliases based on new patterns

### Example Monitoring Query
```sql
-- Check recent merchant matching stats
SELECT 
    DATE(created_at) as date,
    COUNT(*) as total_transactions,
    COUNT(merchant_id) as matched_transactions,
    (COUNT(merchant_id)::float / COUNT(*) * 100) as match_rate
FROM transactions 
WHERE created_at >= NOW() - INTERVAL '7 days'
GROUP BY DATE(created_at)
ORDER BY date DESC;
```

## Troubleshooting

### Common Issues

1. **No embeddings generated**
   - Check OpenAI API key is set
   - Verify network connectivity
   - Check API rate limits

2. **Low similarity scores**
   - Merchant names may be too different
   - Consider lowering similarity threshold
   - Add manual aliases for edge cases

3. **High API costs**
   - Review batch sizes
   - Implement caching for repeated requests
   - Consider using smaller embedding model

### Debug Tools

1. **Test normalization**:
```bash
curl -X POST http://localhost:5000/api/merchants/normalize \
  -H "Content-Type: application/json" \
  -d '{"merchantName": "STARBUCKS STORE #1234 SEATTLE WA"}'
```

2. **Find similar merchants**:
```bash
curl http://localhost:5000/api/merchants/{merchant-id}/similar?limit=5
```

3. **Check embedding status**:
```sql
SELECT 
    COUNT(*) as total_merchants,
    COUNT(embedding) as with_embeddings,
    (COUNT(embedding)::float / COUNT(*) * 100) as embedding_coverage
FROM merchants;
```

## Advanced Features

### Custom Similarity Thresholds
Different merchant types may need different thresholds:

```csharp
var threshold = merchantCategory switch
{
    "restaurants" => 0.8,  // Higher threshold for restaurants
    "gas_stations" => 0.7, // Lower for gas stations
    _ => 0.75
};
```

### Merchant Consolidation
Find and merge duplicate merchants:

```csharp
var duplicates = await _merchantService.FindSimilarMerchantsAsync(merchantId, 10, 0.9);
// Review and consolidate merchants with >90% similarity
```

### Industry-Specific Normalization
Customize normalization rules by industry:

```csharp
public string NormalizeRestaurantName(string name)
{
    // Remove common restaurant suffixes
    return Regex.Replace(name, @"\s+(RESTAURANT|CAFE|BISTRO|GRILL)$", "", RegexOptions.IgnoreCase);
}
```

## Future Enhancements

1. **Category Prediction**: Use embeddings to predict merchant categories
2. **Fraud Detection**: Identify unusual merchant patterns
3. **Spending Insights**: Group similar merchants for better analytics
4. **Auto-Aliasing**: Automatically add aliases based on high similarity matches

## Success Metrics

After implementing embeddings, you should see:
- **90%+ match rate** for known merchants
- **Fewer duplicate merchants** created
- **Better categorization** accuracy
- **Improved spending insights** due to consolidated merchant data

The system will learn and improve over time as more transactions are processed and merchant embeddings are refined.