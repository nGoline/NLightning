# Web Assembly / Blazor

This library provides a decoder/encoder for the BOLT11 invoice format used in the Lightning Network.
It can be used to decode and/or encode BOLT11 invoices in a WebAssembly app.

## Sample

A sample project using this project in a Blazor WebAssembly environment can be
found [here](https://github.com/ngoline/NLightning-Samples/tree/main/Bolt11.Decoder.Blazor).

A live version of the sample can be found at [bolt11.nlightn.ing](https://bolt11.nlightn.ing)

## Usage

Follow the steps below to install and decode bolt11 invoices.

### Installation

Install the package from NuGet:

```bash
dotnet add package NLightning.Bolt11.Blazor
```

### Decoding

Blazor apps need to initialize the CryptoProvider to load the necessary js files. Add the following to your
`Program.cs` file.

#### Initialize the CryptoProvider (libsodium.js)

```csharp
// Add the using directive
using NLightning.Infrastructure.Crypto.Providers.JS;

// ...
// Your app code

// Initialize the Crypto Provider just before starting the server.
await BlazorCryptoProvider.InitializeBlazorCryptoProviderAsync();

await builder.Build().RunAsync();
```

#### Decode the invoice

```blazor
@using NLightning.Bolt11.Models

<button @onclick="DecodeInvoice">Decode</button>
<br/>
@if (invoice != null)
{
    <p>Payment Hash: @invoice.PaymentHash</p>
    <p>Amount MilliSats: @invoice.Amount.MilliSatoshi</p>
    <p>Amount Sats: @invoice.Amount.Satoshi</p>
    <p>Amount BTC: @invoice.Amount</p>
    <p>Description: @invoice.Description</p>
}

@code{

    Invoice? invoice;
    
    void DecodeInvoice()
    {
        invoice = Invoice.Decode("lnbc1pndpjfppp5qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqssp5qqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqqsdqq5243a4h29w7lm6g89hktd0qzfakevjp7hktskal5p69jxa6vyqw4s95577lltw0t6l9dhp7cfld9urkxfsucsxascnxdqmanrlklsqcp5nwzmf");
    }

}
```