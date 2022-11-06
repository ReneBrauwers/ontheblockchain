# ontheblockchain
this project relies on DAPR. For local testing ensure to configure the following state stores
- projects-config        
- projectvotes-config

## Project: VoteScanner
Local-testing with dapr
- execute command "dapr run --app-id votescanner --app-port 4000 --dapr-http-port 5275 dotnet run", this will expose the APIs on port 5275 over plain HTTP
