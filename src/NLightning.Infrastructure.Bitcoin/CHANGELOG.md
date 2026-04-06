# Changelog

All notable changes to this project will be documented in this file.

## v1.0.0

Major release adding wallet address management, funding transaction building, and comprehensive key management improvements.

### Added

- Added `FundingTransactionBuilder` and `IFundingTransactionBuilder` for constructing and signing funding transactions from UTXO sets;
- Added `BitcoinChainService` and `IBitcoinChainService` for chain-level Bitcoin operations;
- Added `IBitcoinWalletService` with `GetUnusedAddressAsync`;
- Added `IBlockchainMonitor.WatchBitcoinAddress` for watching wallet addresses;
- Added `IBlockchainMonitor.PublishAndWatchTransactionAsync` for broadcasting and tracking transactions;
- Added `IBlockchainMonitor.LastProcessedBlockHeight` property;
- Added `IBlockchainMonitor.OnWalletMovementDetected` event;
- Added `SecureKeyManager.ChannelKeyPath`, `DepositP2TrKeyPath`, `DepositP2WpkhKeyPath` key paths;
- Added `SecureKeyManager.OutputDepositP2TrDescriptor`, `OutputDepositP2WshDescriptor`, `OutputChangeP2TrDescriptor`, `OutputChangeP2WshDescriptor` output descriptors;
- Added `LocalLightningSigner` dependency on `IUtxoMemoryRepository` for UTXO selection;

### Changed

- `BitcoinWalletService` rewritten to use `IBlockchainMonitor` and `ISecureKeyManager` instead of direct RPC; no longer implements `IBitcoinWallet`;
- `SecureKeyManager.KeyPath` renamed to `ChannelKeyPath`; `OutputDescriptor` renamed to `OutputChannelDescriptor`;
- `CommitmentKeyDerivationService.DeriveRemoteCommitmentKeys` signature simplified: removed `localChannelKeyIndex` and `commitmentNumber` parameters;
- `DustService` now references `Domain.Protocol.Interfaces` instead of `Domain.Protocol.Services`;
- Updated `PackageProjectUrl` to `https://docs.nlightn.ing`;
- Updated `MessagePack` to `v3.1.4`, `NBitcoin` to `v9.0.5`, `NBitcoin.Secp256k1` to `v3.2.0`, `NetMQ` to `v4.0.2.2`;

### Removed

- Removed `IBitcoinWallet` interface (replaced by `IBitcoinWalletService`);

### Breaking Changes

- Dropped `net8.0` and `net9.0` targets; the library now requires **.NET 10.0** or later;
- `IBitcoinWallet` removed; use `IBitcoinWalletService` instead;
- `SecureKeyManager.KeyPath` → `ChannelKeyPath`; `OutputDescriptor` → `OutputChannelDescriptor`;
- `CommitmentKeyDerivationService.DeriveRemoteCommitmentKeys` signature changed: `localChannelKeyIndex` and `commitmentNumber` parameters removed;
- `LocalLightningSigner` constructor now requires `IUtxoMemoryRepository`;

## v0.0.4

Bump version to use `NLightning.Infrastructure@v1.0.3`.

## v0.0.3

Bump version to use `NLightning.Infrastructure@v1.0.2`.

## v0.0.2

Bump version to use `NLightning.Infrastructure@v1.0.1`.

## v0.0.1

Initial release