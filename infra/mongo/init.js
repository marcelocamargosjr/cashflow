// Cashflow Consolidation — Mongo bootstrap.
// Mounted as /docker-entrypoint-initdb.d/init.js on first startup.
// Schema and indexes per `05-DADOS.md §2.2`.

db = db.getSiblingDB('cashflow_consolidation');

db.createCollection('daily_balances');
db.daily_balances.createIndex(
    { merchantId: 1, date: -1 },
    { name: 'ix_merchant_date' }
);
db.daily_balances.createIndex(
    { lastUpdatedAt: -1 },
    { name: 'ix_last_updated' }
);

db.createCollection('processed_events');
db.processed_events.createIndex(
    { _id: 1 },
    { name: 'pk_event' }
);
db.processed_events.createIndex(
    { processedAt: 1 },
    { name: 'ttl_processed_events', expireAfterSeconds: 604800 }  // 7 days
);
