// using NBitcoin;
//
// namespace NLightning.Infrastructure.Bitcoin.Transactions;
//
// using Comparers;
// using Domain.Bitcoin.Transactions;
// using Domain.Bitcoin.ValueObjects;
// using Domain.Money;
// using Domain.Transactions.Constants;
// using Outputs;
//
// public abstract class BaseTransaction : ITransaction
// {
//     #region Private Fields
//
//     private readonly bool _hasAnchorOutput;
//     private readonly TransactionBuilder _builder;
//     private readonly List<(Coin, Sequence)> _coins = [];
//
//     private readonly Transaction _transaction;
//
//     #endregion
//
//     #region Protected Properties
//
//     protected List<BaseOutput> Outputs { get; private set; } = [];
//     protected LightningMoney CalculatedFee { get; } = LightningMoney.Zero;
//     protected bool Finalized { get; private set; }
//
//     protected Transaction FinalizedTransaction => Finalized
//                                                       ? _transaction
//                                                       : throw new Exception("Transaction not finalized.");
//
//     #endregion
//
//     #region Public Properties
//
//     public TxId TxId { get; private set; } = uint256.Zero.ToBytes();
//
//     public bool IsValid => Finalized
//                                ? _builder.Verify(_transaction)
//                                : throw new Exception("Transaction not finalized.");
//
//     #endregion
//
//     #region Constructors
//
//     protected BaseTransaction(bool hasAnchorOutput, Network network, uint version, SigHash sigHash, params Coin[] coins)
//     {
//         _hasAnchorOutput = hasAnchorOutput;
//
//         _builder = network.CreateTransactionBuilder();
//         _builder.SetSigningOptions(sigHash, false);
//         _builder.DustPrevention = false;
//         _builder.SetVersion(version);
//
//         _coins = coins.Select(c => (c, Sequence.Final)).ToList();
//
//         _transaction = Transaction.Create(network);
//         _transaction.Version = version;
//         _transaction.Inputs.AddRange(_coins.Select(c => new TxIn(c.Item1.Outpoint)));
//     }
//
//     protected BaseTransaction(bool hasAnchorOutput, Network network, uint version, SigHash sigHash,
//                               params (Coin, Sequence)[] coins)
//     {
//         _hasAnchorOutput = hasAnchorOutput;
//
//         _builder = network.CreateTransactionBuilder();
//         _builder.SetSigningOptions(sigHash, false);
//         _builder.DustPrevention = false;
//         _builder.SetVersion(version);
//
//         _coins.AddRange(coins);
//
//         _transaction = Transaction.Create(network);
//         _transaction.Version = version;
//         foreach (var (coin, sequence) in _coins)
//         {
//             _transaction.Inputs.Add(coin.Outpoint, null, null, sequence);
//         }
//     }
//
//     #endregion
//
//     #region Abstract Methods
//
//     internal abstract void ConstructTransaction(LightningMoney currentFeePerKw);
//
//     #endregion
//
//     #region Protected Methods
//
//     protected void SetLockTime(LockTime lockTime)
//     {
//         _transaction.LockTime = lockTime;
//     }
//
//     protected LightningMoney TotalInputAmount => _coins.Sum(c => LightningMoney.Satoshis(c.Item1.Amount));
//
//     protected LightningMoney TotalOutputAmount => Outputs.Sum(o => o.Amount);
//
//     protected bool CheckTransactionAmounts(LightningMoney? fees = null)
//     {
//         // Check if the output amount is greater than the input amount
//         return TotalOutputAmount + (fees ?? LightningMoney.Zero) <= TotalInputAmount;
//     }
//
//     protected void CalculateAndCheckFees(LightningMoney currentFeePerKw)
//     {
//         // Calculate transaction fee
//         CalculateTransactionFee(currentFeePerKw);
//
//         // Check if the output amount plus fees is greater than the input amount
//         if (!CheckTransactionAmounts(CalculatedFee))
//             throw new InvalidOperationException("Output amount cannot exceed input amount.");
//     }
//
//     protected void CalculateTransactionFee(LightningMoney currentFeePerKw)
//     {
//         var outputWeight = CalculateOutputWeight();
//         var inputWeight = CalculateInputWeight();
//
//         CalculatedFee.Satoshi = (outputWeight + inputWeight) * currentFeePerKw.Satoshi / 1000L;
//     }
//
//     #region Weight Calculation
//
//     protected int CalculateOutputWeight()
//     {
//         var outputWeight = WeightConstants.TransactionBaseWeight;
//         if (_hasAnchorOutput)
//         {
//             outputWeight += 8; // Add 8 more bytes for (count_tx_out * 4)
//         }
//
//         foreach (var output in Outputs)
//         {
//             switch (output)
//             {
//                 case FundingOutput:
//                     outputWeight += WeightConstants.P2WshOutputWeight;
//                     break;
//                 case ChangeOutput changeOutput when changeOutput.ScriptPubKey.IsScriptType(ScriptType.P2PKH):
//                     outputWeight += WeightConstants.P2PkhOutputWeight;
//                     break;
//                 case ChangeOutput changeOutput when changeOutput.ScriptPubKey.IsScriptType(ScriptType.P2SH):
//                     outputWeight += WeightConstants.P2ShOutputWeight;
//                     break;
//                 case ChangeOutput changeOutput when changeOutput.ScriptPubKey.IsScriptType(ScriptType.P2WPKH):
//                     outputWeight += WeightConstants.P2WpkhOutputWeight;
//                     break;
//                 case ChangeOutput changeOutput when changeOutput.ScriptPubKey.IsScriptType(ScriptType.P2WSH):
//                     outputWeight += WeightConstants.P2WshOutputWeight;
//                     break;
//                 case ChangeOutput changeOutput:
//                     outputWeight += changeOutput.ScriptPubKey.Length;
//                     break;
//                 case ToLocalOutput:
//                 case ToRemoteOutput when _hasAnchorOutput:
//                     outputWeight += WeightConstants.P2WshOutputWeight;
//                     break;
//                 case ToRemoteOutput:
//                     outputWeight += WeightConstants.P2WpkhOutputWeight;
//                     break;
//                 case ToAnchorOutput:
//                     outputWeight += WeightConstants.AnchorOutputWeight;
//                     break;
//                 case OfferedHtlcOutput:
//                 case ReceivedHtlcOutput:
//                     outputWeight += WeightConstants.HtlcOutputWeight;
//                     break;
//             }
//         }
//
//         return outputWeight;
//     }
//
//     protected int CalculateInputWeight()
//     {
//         var inputWeight = 0;
//         var mustAddWitnessHeader = false;
//
//         foreach (var (coin, _) in _coins)
//         {
//             var input = _transaction.Inputs.SingleOrDefault(i => i.PrevOut == coin.Outpoint)
//                      ?? throw new NullReferenceException("Input not found in transaction.");
//
//             if (input.WitScript.PushCount > 0)
//             {
//                 mustAddWitnessHeader = true;
//             }
//
//             if (coin.ScriptPubKey.IsScriptType(ScriptType.P2PKH))
//             {
//                 inputWeight += 4 * Math.Max(WeightConstants.P2PkhInputWeight, input.ToBytes().Length);
//             }
//             else if (coin.ScriptPubKey.IsScriptType(ScriptType.P2SH))
//             {
//                 inputWeight += 4 * Math.Max(WeightConstants.P2ShInputWeight, input.ToBytes().Length);
//                 inputWeight += input.WitScript.ToBytes().Length;
//             }
//             else if (coin.ScriptPubKey.IsScriptType(ScriptType.P2WPKH))
//             {
//                 inputWeight += 4 * Math.Max(WeightConstants.P2WpkhInputWeight, input.ToBytes().Length);
//                 inputWeight += input.WitScript.ToBytes().Length;
//             }
//             else if (coin.ScriptPubKey.IsScriptType(ScriptType.P2WSH))
//             {
//                 inputWeight += 4 * Math.Max(WeightConstants.P2WshInputWeight, input.ToBytes().Length);
//                 inputWeight += Math.Max(WeightConstants.MultisigWitnessWeight, input.WitScript.ToBytes().Length);
//             }
//             else
//             {
//                 inputWeight += 4 * Math.Max(WeightConstants.P2UnknownInputWeight, input.ToBytes().Length);
//                 inputWeight += input.WitScript.ToBytes().Length;
//             }
//         }
//
//         if (mustAddWitnessHeader)
//         {
//             inputWeight += WeightConstants.WitnessHeader;
//         }
//
//         return inputWeight;
//     }
//
//     #endregion
//
//     #region Input Management
//
//     protected void AddCoin(Coin coin, Sequence sequence)
//     {
//         ArgumentNullException.ThrowIfNull(coin);
//
//         _coins.Add((coin, sequence));
//         _transaction.Inputs.Add(coin.Outpoint, null, null, sequence);
//     }
//
//     protected void AddCoin(Coin coin)
//     {
//         ArgumentNullException.ThrowIfNull(coin);
//
//         _coins.Add((coin, Sequence.Final));
//         _transaction.Inputs.Add(coin.Outpoint, null, null, Sequence.Final);
//     }
//
//     protected void RemoveCoin(Coin coin, Sequence sequence)
//     {
//         ArgumentNullException.ThrowIfNull(coin);
//
//         var indexToRemove = _coins.FindIndex(c => c.Item1.Equals(coin));
//         _coins.RemoveAt(indexToRemove);
//
//         indexToRemove = _transaction.Inputs.FindIndex(i => i.PrevOut == coin.Outpoint);
//         _transaction.Inputs.RemoveAt(indexToRemove);
//     }
//
//     protected void RemoveCoin(Coin coin)
//     {
//         ArgumentNullException.ThrowIfNull(coin);
//
//         var indexToRemove = _coins.FindIndex(c => c.Item1.Outpoint.Equals(coin.Outpoint));
//         _coins.RemoveAt(indexToRemove);
//
//         indexToRemove = _transaction.Inputs.FindIndex(i => i.PrevOut == coin.Outpoint);
//         _transaction.Inputs.RemoveAt(indexToRemove);
//     }
//
//     #endregion
//
//     #region Output Management
//
//     protected void AddOutput(BaseOutput baseOutput)
//     {
//         ArgumentNullException.ThrowIfNull(baseOutput);
//
//         Outputs.Add(baseOutput);
//     }
//
//     protected void AddOutputRange(IEnumerable<BaseOutput> outputs)
//     {
//         ArgumentNullException.ThrowIfNull(outputs);
//
//         var outputBases = outputs as BaseOutput[] ?? outputs.ToArray();
//         if (outputBases.Length == 0)
//             return;
//
//         foreach (var output in outputBases)
//         {
//             ArgumentNullException.ThrowIfNull(output);
//             Outputs.Add(output);
//         }
//     }
//
//     protected void ClearOutputsFromTransaction()
//     {
//         _transaction.Outputs.Clear();
//     }
//
//     protected void RemoveOutput(BaseOutput? baseOutput)
//     {
//         ArgumentNullException.ThrowIfNull(baseOutput);
//
//         Outputs.Remove(baseOutput);
//     }
//
//     protected void AddOrderedOutputsToTransaction()
//     {
//         // Clear TxOuts
//         _transaction.Outputs.Clear();
//
//         switch (Outputs.Count)
//         {
//             case 0:
//                 return;
//             case 1:
//                 _transaction.Outputs.Add(Outputs[0].ToTxOut());
//                 break;
//             default:
//                 // Add ordered outputs
//                 Outputs = Outputs.OrderBy(o => o, TransactionOutputComparer.Instance).ToList();
//                 _transaction.Outputs.AddRange(Outputs.Select(o => o.ToTxOut()));
//                 break;
//         }
//     }
//
//     #endregion
//
//     #endregion
// }