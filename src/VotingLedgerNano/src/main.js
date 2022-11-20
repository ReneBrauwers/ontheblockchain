import 'core-js/actual';
import AppXrp from "@ledgerhq/hw-app-xrp";
import TransportWebHID from "@ledgerhq/hw-transport-webhid";
import RippleCodec from 'ripple-binary-codec';

const xrpl = require('xrpl');

//init
const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))
//extract query string params
var params = (new URL(document.location)).searchParams;
const _votingId = params.get("id") ?? '696e7369676874732d6f6e2d7468652d626c6f636b636861696e';
const _voteCastOn = params.get("vote") ?? '636f6e67726174756c6174696f6e73';
const _destination = params.get("dest") ?? 'rLrauyao2wWCHgf7qnd3JFPhz4r499HvPv';

//decode hex to utf8
const votingId = Buffer.from(_votingId,'hex').toString('utf8');
const voteCastOn = Buffer.from(_voteCastOn,'hex').toString('utf8');



//update UI display with passed in information
//document.getElementById("vote-info").innerHTML = `You are about to vote for ${voteCastOn} see instructions below`;
document.getElementById("vote-name").innerHTML = votingId;
document.getElementById("voting-on").innerHTML = voteCastOn;


async function establishConnection() {
    progressLog(`establishing connection to ledger`,false);

    
    var HidObj = await TransportWebHID.openConnected()
    .catch(e => 
        {
            TransportWebHID.close;
            alert(e.message);
            location.reload();           
        });
    
    //if null then there is no device found
    if(isEmptyObject(HidObj))
    {
        alert("Please connect your ledger nano");
        location.reload();
    }
    else
    {
        //connect to the xrp application
        try
        {
        xrpApp = new AppXrp(HidObj);
        progressLog(`established connection to ledger`);
        return xrpApp;
        }
        catch(ex)
        {
            alert("Please ensure to open the XRP application on your ledger")
            location.reload();

        }
    }
}


async function prepareAndSign(xrp) {
    //init 
   var txResult = {
    "Status":"unknown",
    "TxHash":"unknown"
   };

 try
 {
    var deviceData = await xrp.getAddress("44'/144'/0'/0/0");
   

    if(!isEmptyObject(deviceData))
    {
      var tx = await submitSignedTransaction(xrp,deviceData);
      if(!isEmptyObject(tx))
      {
          
        txResult.Status = (tx.result.meta.TransactionResult == 'tesSUCCESS'? 'success':'failed');
        txResult.TxHash = tx.result.hash;
        progressLog(`Transaction ${txResult.Status} - https://xrpscan.com/tx/${txResult.TxHash}`,false);      
      }
    }
 }
 catch(ex)
 {

 }

 return txResult;

    
}


async function submitSignedTransaction(xrp, deviceData) {

    // Define the network client
    const rippleServer = 'wss://s1.ripple.com/';
  
  const client = new xrpl.Client(rippleServer)
  await client.connect()
  progressLog(`connected to ${rippleServer}`,false);   
  progressLog(`preparing transaction`,false);  


const prepared = await client.autofill({
    "TransactionType": "Payment",
    "Account": deviceData.address,
    "Amount": "1",
    "Fee": "12",
    "Destination": _destination,
    "SigningPubKey": deviceData.publicKey.toUpperCase(),
    "Flags": 2147483648,   
    "Memos": [
        {
            "Memo": {
                "MemoData": _votingId
            }
        },
        {
            "Memo": {
                "MemoData": _voteCastOn
            }
        }
    ]
  });
  
  
  
  const max_ledger = prepared.LastLedgerSequence
    
    const transactionBlob = RippleCodec.encode(prepared);
    //return transactionJSON;
    progressLog(`sign transaction on device`,false);     
    var signedTransaction = await xrp.signTransaction("44'/144'/0'/0/0", transactionBlob);
    //add signature to transaction
    prepared.TxnSignature = signedTransaction;  
    progressLog(`submitting SIGNED transaction to XRPL`,false); 
    const tx = await client.submitAndWait(prepared)
    .catch(e=> 
        {
            //console.log(e.message);
            progressLog(e.message,true);   
        })
 

  client.disconnect()


  //return transaction
  return tx;
}

function isEmptyObject(obj){
    var str = JSON.stringify(obj);    
    return str === '{}';
}
function progressLog(msg,isError)
{
    const $el = document.createElement("li");
    $el.className = "d-flex align-items-start mb-1 small"
    
    if(isError)
    {
        $el.style.color = "#f66";
    }

    $el.textContent = String(msg);
    document.getElementById("status").appendChild($el);
}


 document.getElementById("btn-vote").onclick = async function () 
 {
  try {
   
    //establish connection, if error do a page refresh
    var xrpAppConnection = await establishConnection();
    if(!isEmptyObject(xrpAppConnection))
    {
       
        result = await prepareAndSign(xrpAppConnection)
        .catch(e => {
            console.log(e.message);
            progressLog(e.message,true);   
        })
        
        if(result.Status != 'unknown')
        {
            const el = document.createElement('a');
            const link = document.createTextNode(`Voting ${result.Status}`);
            el.appendChild(link);
            el.className = "btn btn-success";
            el.href = `https://xrpscan.com/tx/${result.TxHash}`;
            el.setAttribute("target","_blank");
            el.setAttribute("role","button")
           
           
            //<a class="btn btn-success" href="https://xrpscan.com/tx/" target="_blank" role="button">Link</a>
            if(result.Status == 'failed')
            {
                dynEl.className = "alert alert-danger" 
            }

           
           
            document.getElementById("finalstate").appendChild(el);
            
        }
        else
        {
            alert('Something went wrong submitting the transaction')
            location.reload();
        }
    }
    //else
    //{
       // alert("Please ensure to connect your ledger. Try again");
        //location.reload();
    //}
   
   // result = establishConnection()
   // .then(xrp => prepareAndSign(xrp))
   // .then(async signature => 
   //     {            
   //         await submitTransaction(signature);
   // })
   // .catch(e => {
   //     console.log(e.message);
   //     progressLog(e.message,true);   
   // });    
 
  } catch (e) {

    //Catch any error thrown and displays it on the screen
    console.log(e.message);
    progressLog(e.message,true);   
  }
}

