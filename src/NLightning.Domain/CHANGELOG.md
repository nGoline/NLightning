# Changelog

All notable changes to this project will be documented in this file.

## v2.0.0

Major release introducing the Client domain, wallet domain types, channel open validation, and comprehensive interface updates.

### Added

- Added `Client` domain namespace with `INamedPipeIpcService`, `ClientException`, `ErrorCodes`, `ClientCommand`,
  `OpenChannelClientRequest`, `OpenChannelClientSubscriptionRequest`, `OpenChannelClientResponse`, and
  `OpenChannelClientSubscriptionResponse`;
- Added Bitcoin wallet types: `UtxoModel`, `WalletAddressModel`, `AddressType`, `KeyConstants`,
  `IUtxoDbRepository`, `IUtxoMemoryRepository`, `IWalletAddressesDbRepository`;
- Added `FundingTransactionModel`, `FundingTransactionModelFactory`, and `IFundingTransactionModelFactory`;
- Added `WalletMovementEventArgs` for wallet event propagation;
- Added `IChannelOpenValidator` interface with `ChannelOpenValidator`, `ChannelOpenMandatoryValidationParameters`,
  and `ChannelOpenOptionalValidationParameters`;
- Added `ChannelUpdatedEventArgs` and `ChannelUpgradedEventArgs` channel events;
- Added `NodeConstants` and `AttentionMessageEventArgs` to the node domain;
- Added `GetDepositP2TrKeyAtIndex` and `GetDepositP2WpkhKeyAtIndex` methods to `ISecureKeyManager`;

### Changed

- Updated `Feature` enum and `FeatureSet` defaults to reflect latest BOLT spec
  ([lightning/bolts#1310](https://github.com/lightning/bolts/pull/1310)): `OptionDataLossProtect`,
  `VarOnionOptin`, `OptionStaticRemoteKey`, and `PaymentSecret` are now set as compulsory by default;
- Simplified `FeatureSet` dependency rules, removing several previously required feature dependencies;
- `IPeerManager.ConnectToPeerAsync` now returns `Task<PeerModel>` instead of `Task`;
- `IPeerManager.DisconnectPeer` now accepts an optional `Exception` parameter;
- `ISecureKeyManager.KeyPath` renamed to `ChannelKeyPath`; `GetNextKey`/`GetKeyAtIndex` renamed to
  `GetNextChannelKey`/`GetChannelKeyAtIndex`;
- `LightningMoney.ToString()` now includes the unit by default;
- Updated `PackageProjectUrl` to `https://docs.nlightn.ing`;

### Fixed

- Fixed `AcceptChannel1Payload.ToSelfDelay` propagation in channel config creation;

### Removed

- Removed `Feature.InitialRoutingSync` (deprecated in latest BOLT spec);
- Removed `Feature.OptionAnchorOutputs` (superseded by `OptionAnchorsZeroFeeHtlcTx`);

### Breaking Changes

- Dropped `net8.0` and `net9.0` targets; the library now requires **.NET 10.0** or later;
- `ISecureKeyManager`: `KeyPath` → `ChannelKeyPath`; `GetNextKey`/`GetKeyAtIndex` → `GetNextChannelKey`/`GetChannelKeyAtIndex`;
- `OpenChannel1Message`: `ChannelTypeTlv` is now required (non-nullable); constructor parameter order changed;
- `ChannelConfig.ChannelReserveAmount` changed from `LightningMoney?` to `LightningMoney`;
- `Feature.InitialRoutingSync` and `Feature.OptionAnchorOutputs` removed from the `Feature` enum;
- `LightningMoney.ToString()` output now includes the unit;

## v1.1.2

This version removes a Span size check from `BitReader` since the calculations are complex and should be done by the
caller.

### Removed

- Removed the Span size check from `BitReader` since it was breaking the expected flow.

## v1.1.1

This version fixes a behavior on `BitReader` where reading unaligned bits near the end of the buffer would result in
exception;

### Fixed

- Fixed `BitReader` behavior where reading unaligned bits near the end of the buffer would result in exception;

## v1.1.0

This version introduces the ability to register and manage custom Bitcoin-like networks at runtime, enhancing the
flexibility and extensibility of the `BitcoinNetwork` class. This allows developers to define and work with networks
beyond the standard `mainnet`, `testnet`, and `regtest`, making the library more adaptable to various use cases and
testing scenarios.

### Added

- **Extensible Custom Bitcoin Networks:**  
  `BitcoinNetwork` now supports registration and management of custom network names and corresponding `ChainHash` values
  at runtime.
    - Implementers can register any custom Bitcoin-like network and have it fully supported throughout the domain layer.
    - Provides methods to register and unregister custom networks and retrieve their `ChainHash` from name, with all
      conversions and equality logic preserved.
    - Built-in networks (`mainnet`, `testnet`, `regtest`) remain unchanged.

## v1.0.0

This version's focus has been on enhancing Bitcoin integration, refining channel management, standardizing protocol
message handling, and improving core domain primitives. These changes aim to solidify the domain logic and provide a
richer, more robust foundation for building Lightning Network features.

### Added

- Added new namespaces, interfaces, and value objects;
- Introduced internal value objects and models to represent Bitcoin-specific concepts (e.g., `TxId`, `BitcoinScript`,
  `BitcoinLockTime`, `CompactPubKey`, `Secret`) to enhance abstraction and reduce external library coupling;
- Introduced comprehensive models for channel components like `ChannelModel`, `ChannelKeySetModel`, and `Htlc`;
- Added detailed value objects for channel configuration (`ChannelConfig`), basepoints (`ChannelBasepoints`), and
  signing information (`ChannelSigningInfo`);
- Introduced new factories and interfaces for message creation and serialization;
- Added various factories (e.g., `ChannelFactory`, `CommitmentTransactionModelFactory`)  and service interfaces (e.g.,
  `ILightningSigner`, `ICommitmentKeyDerivationService`, `ISecretStorageService`) to support the new domain models and
  promote better separation of concerns;
- Moved `BitReader` and `BitWriter` utilities from `NLightning.Common`.

### Modified

- Significant updates to arithmetic operations on `LightningMoney`;
- Updated feature handling logic on `FeatureSet`;
- Adjustments in `NodeOptions.cs` and `FeatureOptions.cs`;
- Restructured protocol messages and payloads (e.g., `OpenChannel1Message`, `AcceptChannel1Payload`) with dedicated
  properties and TLV field support;
- Update existing message and payload classes;
- Moved `Witness` to `NLightning.Domain/Bitcoin/ValueObjects/`;
- Moved `ChannelFlags` to `NLightning.Domain/Channels/ValueObjects/`;
- Moved `ShortChannelId` to `NLightning.Domain/Channels/ValueObjects/`;
- Moved `BigSize` to `NLightning.Domain/Protocol/ValueObjects/`;
- Moved `ChainHash` to `NLightning.Domain/Protocol/ValueObjects/`;
- Moved service interfaces from `Services` to `Interfaces`;
- Serialization interfaces moved to `NLightning.Domain.Serialization.Interfaces`;
- Utility interfaces moved to `NLightning.Domain.Utils.Interfaces`.

### Deleted

- Removed direct dependencies on `NBitcoin` and `NBitcoin.Secp256k1`.
- Removed interfaces `ICommitmentTransactionFactory`, `IFundingTransactionFactory`,  `IHtlcTransactionFactory`,
  `IPingPongServiceFactory`, `IHtlc`.

### Breaking Changes

- `Channel` has moved to `ChannelModel`;
- `HashConstants` has been merged into `Crypto.Constants.CryptoConstants`;
- `ChannelState` has been moved to `NLightning.Domain.Channels.Enums`;
- `ChannelException` has been replaced by more specific exceptions like `ChannelErrorException` and
  `ChannelWarningException`;
- `IMessageFactory` has been moved to `NLightning.Domain.Protocol.Interfaces.IMessageFactory`;
- `TransactionConstants` and `WeightConstants` moved to `NLightning.Domain.Bitcoin.Transactions.Constants`;
- Removed interfaces as per the removed section;
- `ISecureKeyManager` has been moved to `NLightning.Domain.Protocol.Interfaces`;
- Service interfaces in `Services` sub-namespaces (e.g., `IDustService.cs`, `IKeyDerivationService.cs`,
  `ISecretStorageService`)moved to corresponding `Interfaces` namespaces;
- Serialization factory interfaces have been moved to `NLightning.Domain.Serialization.Interfaces`;
- `ChannelId` has been moved to `NLightning.Domain.Channels.ValueObjects/ChannelId.`;
- `IValueObject` has been moved to `NLightning.Domain.Interfaces/IValueObject`;
- `Network` has been moved to `BitcoinNetwork`;
- Many types have been moved to more specific sub-namespaces (e.g., within `Bitcoin`, `Channels`, `Protocol`,
  `Serialization`, `Utils`). This is a breaking change requiring updates to `using` statements.
- The extensive refactoring, addition of new types, and modifications to existing files imply potential changes to
  method signatures, constructors, and property accessors in various parts of the domain.
- The direct package reference to `NBitcoin` has been removed from the project file. Consumers relying on transitive
  NBitcoin types exposed by `NLightning.Domain`'s previous public API may need to add a direct reference to NBitcoin
  or adapt to the new abstractions provided within `NLightning.Domain.Bitcoin.*`.

## v0.0.1

Initial release