# Changelog

All notable changes to this project will be documented in this file.

## v5.0.0

BOLT 11 compliance fix for unknown fallback address versions and updated package URL.

### Fixed

- Fixed `FallbackAddressTaggedField.FromBitReader` to return `null` instead of throwing on unknown witness
  versions, complying with BOLT 11's requirement to skip `f` fields with unknown versions;
- Fixed `Invoice.FallbackAddresses` getter to handle a nullable list correctly;

### Changed

- Updated `PackageProjectUrl` to `https://docs.nlightn.ing`;

### Breaking Changes

- Dropped `net8.0` and `net9.0` targets; the library now requires **.NET 10.0** or later;

## v4.0.5

Fixed an error when decoding `PayeePubKeyTaggedField`.

### Fixed

- Fixed `PayeePubKeyTaggedField.FromBitReader` logic to send the correct-sized array to `BitReader`.

## v4.0.4

Bump version to use `NLightning.Infrastructure@v1.0.2`.

## v4.0.3

Wrongful release that should bump `NLightning.Infrastructure` to fix an error when decoding `PayeePubKeyTaggedField`.

## v4.0.2

Bump version to use `NLightning.Infrastructure@v1.0.1`

## v4.0.1

Fixed `TaggedFieldList` logic to accept zero length description on an invoice.

### Fixed

- Fixed`TaggedFieldList.FromBitReader` logic to remove the check for zero length;

## v4.0.0

Introduced a dedicated validation service and stricter field checks to improve invoice robustness, along with API
consistency and null-safety enhancements.

### Added

- Added `InvoiceValidationService` for enhanced validation during invoice decoding;
- Improved error handling with detailed `ArgumentException` messages for missing/invalid fields.
- Added [DisallowNull] on `PaymentHash`, `RoutingInfos`, `Features`, `FallbackAddresses`, `PaymentSecret`,
  `PayeePubKey`, and `MinFinalCltvExpiry` for better nullability handling;
- Added `[MaybeNullWhen(false)]` annotation to `TaggedFieldList.TryGet<T>` method for better nullability handling;

### Modified

- Modified `TaggedFieldList.Add` method to perform validation before adding new tagged fields;
- Modified `DescriptionHashTaggedField.IsValid` to return false if the `DescriptionHash` is all ones or all zeroes;
- Modified `DescriptionTaggedField` constructor and `IsValid` method to ensure the description is under
  `MaxDescriptionBytes` when converted to a UTF8 byte array;
- Modified `PaymentHashTaggedField.IsValid` to return false if the `PaymentHash` is all ones or all zeroes;
- Modified `PaymentSecretTaggedField.IsValid` to return false if the `PaymentSecret` is all ones or all zeroes;

### Breaking Changes

- Changed `Invoice` constructors to receive a `BitcoinNetwork` instead of `Network` to align with `NLightning` domain;

## v3.0.1

Upgrade `NLightning.Infrastructure` to `v0.0.2` to fix an issue with `staticwebassets` not being copied to the
output directory on publish.

### Changed

- Upgraded `NLightning.Infrastructure` to `v0.0.2`.

## v3.0.0

Moved all necessary classes to `NLightning.Bolt11` project.

### Added

- Added all base classes to the `NLightning.Bolt11` project;
- Added dependency to `NLightning.Infrastructure`.

### Breaking Changes

- All namespaces have been changed to conform to the new project structure.

## v2.0.0

Removal of `SecureKeyManager` static class and introduction of `ISecureKeyManager` interface.

Introduction of `LightningMoney` class to represent amounts in the invoice.

### Removed

- Removed `SecureKeyManager` class.

### Added

- Added `ISecureKeyManager` interface;
- Added `ToString(Key nodeKey)` method to generate the invoice string using a specific key;
- Added `Encode(Key nodeKey)` method to encode the invoice using a specific key.

### Changed

- Invoice public constructors now may receive a `ISecureKeyManager` implementation.

### Breaking Changes

- `Encode` now rely on the `ISecureKeyManager` being passed on the constructors to encode the invoice;
- `ToString` now rely on the `ISecureKeyManager` being passed on the constructors to generate the invoice string;
- Invoice public constructors now receive a `LightningMoney` object instead of `ulong`;
- Removed `AmountMilliSats` and `AmountSats` in favor of the `Amount` property.

## v1.0.0

Removal of `ConfigManager` static class.

### Removed

- Removed `ConfigManager` class;
- Removed obsolete `NLightning.Bolt11.Invoice` class.

### Changed

- Decode method can receive a `Network` object to check if the invoice is valid for the network.

### Breaking Changes

- Invoice public constructors now need to receive a `Network` object;
- Invoice static constructors now need to receive a `Network` object.

## v0.2.4

Update `BitReader` and `BitWriter` to be fully managed

### Changed

- Dropped `unsafe` modifier from `BitReader` and `BitWriter`;
- Refactored `BitReader` and `BitWriter` to use managed objects.

## v0.2.3

Fix Invoice.TaggedFields' Collection item update behavior

### Added

- Added `EventHandler` on `TaggedFieldList`, `RoutingInfoCollection`, and `Features` to inform subscribers upon changes
  in the collection.

### Changed

- `Invoice` subscribes to changes on new `Changed` events from contained Lists.

## v0.2.2

Set `CheckForOverflowUnderflow` to false when package is wasm (Blazor)

### Added

- Added `<CheckForOverflowUnderflow>false</CheckForOverflowUnderflow>` under
  `<PropertyGroup Condition="'$(UsingMicrosoftNETSdkBlazorWebAssembly)' == 'true' Or $(Configuration.Contains('.Wasm'))">`
  on `.csproj` file.

## v0.2.1

Fix Assembly name for blazor package

### Added

- Added `<AssemblyName>NLightning.Bolt11.Blazor</AssemblyName>` under
  `<PropertyGroup Condition="'$(UsingMicrosoftNETSdkBlazorWebAssembly)' == 'true' Or $(Configuration.Contains('.Wasm'))">`
  on `.csproj` file.

## v0.2.0

Add WebAssembly capabilities

### Added

- WebAssembly capabilities to enable running code in the browser;
- New package `NLightning.Bolt11.Blazor` on nuget. _see [readme](README.md#decode-the-invoice) for instructions_

### Changed

- Move Crypto classes from `Bolts` to `Common` and made it public.

### Deprecated

- Deprecated InvoiceProxy `NLightning.Bolt11.Invoice` in favor of using `NLightning.Bolts.BOLT11.Invoice` directly.
  This class will be removed on version `v1.x.x`.

## v0.1.0

Introduction of Invoice Proxy

### Added

- `InvoiceProxy` class to make it easier to import from the `NLightning.Bolt11` package.

## v0.0.1

Initial release