# ontheblockchain
SRC folder contains the following solutions

## VotingOnTheBlockChain
this project relies on DAPR. For local testing ensure to configure the following state stores
- projects-config        
- projectvotes-config

### Project: VoteScanner
Local-testing with dapr
- execute command "dapr run --app-id votescanner --app-port 4000 --dapr-http-port 5275 dotnet run", this will expose the APIs on port 5275 over plain HTTP

## VotingLedgerNano
This project is using NodeJS and allows voting on the XRPL blockchain using a ledger nano.
