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
docker compose build --no-cache
```
