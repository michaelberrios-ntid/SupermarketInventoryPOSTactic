# SupermarketInventoryPOSTactic
Implementation of the POS System Tactic for the Supermarket Inventory class project used in the designed Architecture. 

## Process
When a Sale or Inventory change occurs, and event is created in the POS's local database. The POS Sync Service will then read these events and send them to the Store Inventory API, which will update the store's inventory accordingly. 

## Project Structure
SupermarketInventory/
├── docker-compose.yml
├── POS.ConsoleApp/
│   └── Dockerfile
├── POS.SyncService/
│   └── Dockerfile
├── StoreInventory.API/
│   └── Dockerfile
└── Common/
    └── InventoryEvent.cs

## Run the Docker Compose
```
docker compose down -v
docker compose build --no-cache
docker compose up
```

## On a Separate Terminal, run the POS Console App
```
docker compose run pos_app
```

### Interact with the POS Console App and Perform a Sales transaction
```
Select an Transaction Type:
  Sale
  Refund
  RebuildSchemas
sale
You selected: Sale
Processing Sale transaction...
Scan Product Barcode for Sale: 1001
✔️ Found Organic Apples — Price: $0.99, Stock: 100
Enter quantity to purchase: 20
✅ Product Sale Successful.
```

### On a Separate Terminal, observe the POS Local Database
```
docker run -it --rm \
  -v supermarketinventorypostactic_posdata:/data \
  nouchka/sqlite3 /data/POS_Local.db

SQLite version 3.40.1 2022-12-28 14:03:47
Enter ".help" for usage hints.
sqlite> .tables
SalesTransaction
sqlite> .headers on
sqlite> .mode column
sqlite> select * from SalesTransaction;
id                                    transaction_type  product_id  quantity  price  timestamp                   
------------------------------------  ----------------  ----------  --------  -----  ----------------------------
3925e545-52e9-4dd6-95d2-2cd406ecdfe8  Sale              1001        20        0.99   2025-08-05T10:02:59.8983101Z
```

## Sync Service
The POS Sync Service will automatically read the local database and send the events to the Store Inventory API.
On yet, another terminal, observe the Store Inventory API's database:
```
docker run -it --rm \
  -v supermarketinventorypostactic_storedata:/data \
  nouchka/sqlite3 /data/LocalStore.db

SQLite version 3.40.1 2022-12-28 14:03:47
Enter ".help" for usage hints.
sqlite> select * from SalesTransaction;
## there are no records yet
```

Once the sync service synchronizes every 60 seconds (1 minute), you will see the records in the Store Inventory API's database:
```
sqlite> select * from SalesTransaction;
sqlite> select * from SalesTransaction;
id                                    transaction_type  product_id  quantity  price  timestamp                   
------------------------------------  ----------------  ----------  --------  -----  ----------------------------
c5143608-c7a0-446a-ba47-0ab285c6c170  Sale              1001        20        0.99   2025-08-05T11:07:36.6377297Z
```
At this point, the POS Local Database has the records removed as they've been processed by the sync service.
```
sqlite> select * from SalesTransaction;
## there are no records anymore until a new transaction occurs
```

Additionally, the Store Inventory API's database will also update the Product table to reflect the new stock levels:
```
sqlite> select * from Product WHERE product_id = "1001";
product_id  name            price  quantity
----------  --------------  -----  --------
1001        Organic Apples  0.99   80 
```