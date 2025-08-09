# SupermarketInventoryPOSTactic
Implementation of the POS System Tactic for the Supermarket Inventory class project used in the designed Architecture. 

## Quality Attribute: Reliability
Tactic: Fault Tolerance via Deferred Synchronization

The POS Sync Service is designed to ensure that no sales or return transactions are lost if the StoreInventory API becomes temporarily unavailable.

When the API is unreachable or returns an error, transactions remain in POS_Local.db.

The sync process retries periodically until a successful connection is established.

Only after a confirmed API response does the POS Sync Service remove the transactions from POS_Local.db and transfer them to LocalStore.db.

### Benefit:
This approach guarantees data persistence and transaction integrity despite network or API failures, ensuring the system can continue operating locally without losing records.

## Process
When a Sale or Inventory change occurs, and event is created in the POS's local database. The POS Sync Service will then read these events and send them to the Store Inventory API, which will update the store's inventory accordingly. 

To demonstrate reliability, intentionally stop (crash) the StoreInventory.API service. During this downtime, any new transactions will continue to be saved in the POS Local Database. When the StoreInventory.API becomes available again, the POS Sync Service will automatically detect this and synchronize all pending transactions. The sync service will keep retrying until all transactions are successfully sent to the StoreInventory.API, ensuring no data is lost even during temporary outages.

## Project Structure

```
SupermarketInventory/
├── docker-compose.yml
├── POS.ConsoleApp/
│   └── Dockerfile
├── POS.SyncService/
│   └── Dockerfile
├── StoreInventory.API/
│   └── Dockerfile
└── Common/
  └── Database.cs
  └── Transaction.cs
```

## Run the Docker Compose
```
docker compose down -v
docker compose build --no-cache
docker compose up -d --scale store_api=3
```

### If a Volume is Still in Use (force stop)
```
docker rm -f $(docker ps -aq)
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

## Crashing the StoreInventory.API
To demonstrate the reliability of the POS Sync Service, you can stop the StoreInventory.API service. This will simulate a failure scenario where the API is temporarily unavailable.

On another terminal, you can stop the StoreInventory.API service by running:
```
docker compose stop storeinventory_api
``` 

## Attempt to do a Transacation (Sale or Refund)
Simulate a system failure by killing store_api-1 container:
```
docker kill supermarketinventorypostactic-store_api-1
```

While the StoreInventory.API (store_api-1) is down, you can still perform transactions in the POS Console App.
These transactions will be saved in the POS Local Database and will not be lost.

Go back to the POS Console App terminal and perform another a transaction:
```
Select an Transaction Type:
  Sale
  Refund
sale
You selected: Refund
Processing Sale transaction...
Scan Product Barcode for Sale: 1001
✔️ Found Organic Apples — Price: $0.99, Stock: 100
Enter quantity to purchase: 20
✅ Product Refund Successful.
```

### Killing all containers would results in a failure to sync
```
docker kill supermarketinventorypostactic-store_api-2
docker kill supermarketinventorypostactic-store_api-3
```

## Atttemp to do more transactions
```
Select an Transaction Type:
  Sale
  Return
Your choice: sale
You selected: Sale
Processing Sale transaction...
Scan Product Barcode for Sale: 1001
Unhandled exception. System.Net.Http.HttpRequestException: Resource temporarily unavailable (store_api:8080)
 ---> System.Net.Sockets.SocketException (11): Resource temporarily unavailable
   at System.Net.Sockets.Socket.AwaitableSocketAsyncEventArgs.ThrowException(SocketError error, CancellationToken cancellationToken)
   at System.Net.Sockets.Socket.AwaitableSocketAsyncEventArgs.System.Threading.Tasks.Sources.IValueTaskSource.GetResult(Int16 token)
   at System.Net.Sockets.Socket.<ConnectAsync>g__WaitForConnectWithCancellation|285_0(AwaitableSocketAsyncEventArgs saea, ValueTask connectTask, CancellationToken cancellationToken)
   at System.Net.Http.HttpConnectionPool.ConnectToTcpHostAsync(String host, Int32 port, HttpRequestMessage initialRequest, Boolean async, CancellationToken cancellationToken)
   --- End of inner exception stack trace ---
...
...
```