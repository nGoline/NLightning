# NLightning.Bolt11

This library provides a decoder/encoder for the BOLT11 invoice format used in the Lightning Network.
It can be used to decode and/or encode BOLT11 invoices.

## Available Packages

We've decided to have two packages, one for desktop/server development, and one for Blazor WebAssembly development.

The reason behind this is that for a Blazor app running fully on the browser we don't have access to native libsodium.

### WebAssembly Instructions

For WebAssembly instructions click [here](bolt11/webassembly)

## Usage

Follow the steps below to install and decode bolt11 invoices.

### Installation

Install the package from NuGet:

```bash
dotnet add package NLightning.Bolt11
```

### Decoding

```csharp
// add the using directive
using NLightning.Bolt11.Models;
using NLightning.Domain.ValueObjects;

var expectedNetwork = Network.MAINNET;

// decode the invoice string
var invoice = Invoice.Decode(invoice_string, network);

// Get properties of the invoice
Console.WriteLine("Here's a few props from the invoice:")
Console.WriteLine(invoice.Amount.MilliSatoshi);
Console.WriteLine(invoice.Amount.Satoshi);
Console.WriteLine(invoice.Amount); // In Bitcoin
Console.WriteLine(invoice.PaymentHash);
Console.WriteLine("A list with all the props can be found at: https://docs.nlightn.ing/api/NLightning.Bolts.BOLT11.Invoice.html#properties");
```

### Encoding

```csharp
// add the using directive
using NBitcoin;
using NLightning.Bolt11.Models;
using NLightning.Domain.ValueObjects;
using NLightning.Domain.Money;
using Network = NLightning.Domain.ValueObjects.Network;

// create a new invoice
var invoice = new Invoice(LightningMoney.Satoshis(100), "my description", uint256.One, uint256.Zero, Network.MAINNET);

// get invoice string
var invoiceString = invoice.Encode(new Key());
```