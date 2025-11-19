using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace NLightning.Infrastructure.Repositories.Memory;

using Domain.Bitcoin.Interfaces;
using Domain.Bitcoin.ValueObjects;
using Domain.Bitcoin.Wallet.Models;
using Domain.Channels.ValueObjects;
using Domain.Money;

public class UtxoMemoryRepository : IUtxoMemoryRepository
{
    private readonly ConcurrentDictionary<(TxId, uint), UtxoModel> _utxoSet = [];

    public void Add(UtxoModel utxoModel)
    {
        if (!_utxoSet.TryAdd((utxoModel.TxId, utxoModel.Index), utxoModel))
            throw new InvalidOperationException("Cannot add Utxo");
    }

    public void Spend(UtxoModel utxoModel)
    {
        _utxoSet.TryRemove((utxoModel.TxId, utxoModel.Index), out _);
    }

    public bool TryGetUtxo(TxId txId, uint index, [MaybeNullWhen(false)] out UtxoModel utxoModel)
    {
        return _utxoSet.TryGetValue((txId, index), out utxoModel);
    }

    public LightningMoney GetConfirmedBalance(uint currentBlockHeight)
    {
        return LightningMoney.Satoshis(_utxoSet.Values
                                               .Where(x => x.BlockHeight + 3 <= currentBlockHeight)
                                               .Sum(x => x.Amount.Satoshi));
    }

    public LightningMoney GetUnconfirmedBalance(uint currentBlockHeight)
    {
        return LightningMoney.Satoshis(_utxoSet.Values
                                               .Where(x => x.BlockHeight + 3 > currentBlockHeight)
                                               .Sum(x => x.Amount.Satoshi));
    }

    public LightningMoney GetLockedBalance()
    {
        return LightningMoney.Satoshis(_utxoSet.Values
                                               .Where(x => x.LockedToChannelId is not null)
                                               .Sum(x => x.Amount.Satoshi));
    }

    public void Load(List<UtxoModel> utxoSet)
    {
        foreach (var utxoModel in utxoSet)
            _utxoSet.TryAdd((utxoModel.TxId, utxoModel.Index), utxoModel);
    }

    public List<UtxoModel> LockUtxosToSpendOnChannel(LightningMoney requestFundingAmount, ChannelId channelId)
    {
        // Get available UTXOs (not already locked for other channels)
        var availableUtxos = _utxoSet.Values
                                     .Where(utxo => utxo.LockedToChannelId is null)
                                     .OrderByDescending(utxo => utxo.Amount.Satoshi)
                                     .ToList();

        if (availableUtxos.Count == 0)
            throw new InvalidOperationException("No available UTXOs");

        // Try Branch and Bound to find an exact match or minimize inputs
        var selectedUtxos = BranchAndBound(availableUtxos, requestFundingAmount);

        if (selectedUtxos == null || selectedUtxos.Count == 0)
            throw new InvalidOperationException("Insufficient funds");

        // Lock the selected UTXOs for this channel
        foreach (var selectedUtxo in selectedUtxos)
        {
            selectedUtxo.LockedToChannelId = channelId;
            _utxoSet[(selectedUtxo.TxId, selectedUtxo.Index)] = selectedUtxo;
        }

        return selectedUtxos;
    }

    public List<UtxoModel> GetLockedUtxosForChannel(ChannelId channelId)
    {
        return _utxoSet.Values.Where(x => x.LockedToChannelId.Equals(channelId)).ToList();
    }

    public List<UtxoModel> ReturnUtxosNotSpentOnChannel(ChannelId channelId)
    {
        var utxos = _utxoSet.Values.Where(x => x.LockedToChannelId.Equals(channelId)).ToList();
        foreach (var utxo in utxos)
        {
            utxo.LockedToChannelId = null;
            _utxoSet[(utxo.TxId, utxo.Index)] = utxo;
        }

        return utxos;
    }

    public void ConfirmSpendOnChannel(ChannelId channelId)
    {
        var utxos = _utxoSet.Values.Where(x => x.LockedToChannelId.Equals(channelId));
        foreach (var utxo in utxos)
            _utxoSet.TryRemove((utxo.TxId, utxo.Index), out _);
    }

    private static List<UtxoModel>? BranchAndBound(List<UtxoModel> utxos, LightningMoney targetAmount)
    {
        const int maxTries = 100_000;
        var tries = 0;

        // Best solution found so far
        List<UtxoModel>? bestSelection = null;
        var bestWaste = long.MaxValue;

        // Current selection being explored
        var targetSatoshis = targetAmount.Satoshi;

        // Stack for depth-first search: (index, includeUtxo)
        var stack = new Stack<(int index, bool include, List<UtxoModel> selection, long value)>();
        stack.Push((0, true, [], 0));
        stack.Push((0, false, [], 0));

        while (stack.Count > 0 && tries < maxTries)
        {
            tries++;
            var (index, include, selection, value) = stack.Pop();

            if (include && index < utxos.Count)
            {
                selection = new List<UtxoModel>(selection) { utxos[index] };
                value += utxos[index].Amount.Satoshi;
            }

            // Check if we found a valid solution
            if (value >= targetSatoshis)
            {
                var waste = value - targetSatoshis;

                // Perfect match (changeless transaction)
                if (waste == 0)
                    return selection;

                // Better solution than the current best
                if (waste < bestWaste ||
                    (waste == bestWaste && selection.Count < (bestSelection?.Count ?? int.MaxValue)))
                {
                    bestSelection = new List<UtxoModel>(selection);
                    bestWaste = waste;
                }

                continue; // Prune this branch
            }

            // Move to the next UTXO
            var nextIndex = index + 1;
            if (nextIndex >= utxos.Count)
                continue;

            // Calculate upper bound (current value + all remaining UTXOs)
            var upperBound = value;
            for (var i = nextIndex; i < utxos.Count; i++)
                upperBound += utxos[i].Amount.Satoshi;

            // Prune if we can't reach the target even with all remaining UTXOs
            if (upperBound < targetSatoshis)
                continue;

            // Explore both branches: include and exclude the next UTXO
            stack.Push((nextIndex, false, [.. selection], value));
            stack.Push((nextIndex, true, [.. selection], value));
        }

        // If no exact match found, return the best solution or fallback to greedy
        // Fallback: simple greedy approach if BnB didn't find a solution
        return bestSelection ?? GreedySelection(utxos, targetAmount);
    }

    private static List<UtxoModel>? GreedySelection(List<UtxoModel> utxos, LightningMoney targetAmount)
    {
        var selected = new List<UtxoModel>();
        long currentSum = 0;
        var targetSatoshis = targetAmount.Satoshi;

        foreach (var utxo in utxos)
        {
            selected.Add(utxo);
            currentSum += utxo.Amount.Satoshi;

            if (currentSum >= targetSatoshis)
                return selected;
        }

        return null; // Insufficient funds
    }
}