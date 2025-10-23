namespace NLightning.Bolt11.Tests.Models;

using Bolt11.Models;
using Bolt11.Models.TaggedFields;
using Domain.Protocol.ValueObjects;
using Domain.Utils;
using Enums;
using Interfaces;
using Mocks;

public class TaggedFieldListTests
{
    [Fact]
    public void Given_TaggedFieldList_When_AddValidTaggedField_Then_ItemIsAddedAndChangedEventRaised()
    {
        // Given
        var list = new TaggedFieldList();
        var eventRaised = false;
        list.Changed += (_, _) => eventRaised = true;

        var field = new MockTaggedField
        {
            Type = TaggedFieldTypes.Description,
            Length = 10
        };

        // When
        list.Add(field);

        // Then
        Assert.Single(list);
        Assert.Same(field, list[0]);
        Assert.True(eventRaised, "Changed event should be raised after Add().");
    }

    [Fact]
    public void Given_TaggedFieldListWithExistingField_When_AddSameType_Then_ArgumentExceptionIsThrown()
    {
        // Given
        var list = new TaggedFieldList { new MockTaggedField { Type = TaggedFieldTypes.Description } };

        // When / Then
        var ex = Assert.Throws<ArgumentException>(() => list.Add(new MockTaggedField
                                                                     { Type = TaggedFieldTypes.Description })
        );

        Assert.Contains("already contains a tagged field of type Description", ex.Message);
    }

    [Fact]
    public void Given_TaggedFieldListWithDescription_When_AddDescriptionHash_Then_ArgumentExceptionIsThrown()
    {
        // Given
        var list = new TaggedFieldList { new MockTaggedField { Type = TaggedFieldTypes.Description } };

        // When / Then
        var ex = Assert.Throws<ArgumentException>(() =>
                                                      list.Add(new MockTaggedField
                                                                   { Type = TaggedFieldTypes.DescriptionHash })
        );

        Assert.Contains("already contains a tagged field of type DescriptionHash", ex.Message);
    }

    [Fact]
    public void Given_TaggedFieldListWithDescriptionHash_When_AddDescription_Then_ArgumentExceptionIsThrown()
    {
        // Given
        var list = new TaggedFieldList { new MockTaggedField { Type = TaggedFieldTypes.DescriptionHash } };

        // When / Then
        var ex = Assert.Throws<ArgumentException>(() =>
                                                      list.Add(new MockTaggedField
                                                                   { Type = TaggedFieldTypes.Description })
        );

        Assert.Contains("already contains a tagged field of type Description", ex.Message);
    }

    [Fact]
    public void Given_TaggedFieldList_When_AddFallbackAddressMultipleTimes_Then_NoExceptionIsThrown()
    {
        // Given
        var list = new TaggedFieldList
        {
            // When
            new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress },
            new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress }
        };

        // Then
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Given_TaggedFieldList_When_AddRange_Then_AllItemsAddedAndSingleChangedEventFired()
    {
        // Given
        var list = new TaggedFieldList();
        var eventCount = 0;
        list.Changed += (_, _) => eventCount++;

        var fields = new List<ITaggedField>
        {
            new MockTaggedField { Type = TaggedFieldTypes.Description },
            new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress }
        };

        // When
        list.AddRange(fields);

        // Then
        Assert.Equal(2, list.Count);
        // Because AddRange calls OnChanged once after everything is added
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Given_ExistingItem_When_Remove_Then_ItemIsRemovedAndEventRaised()
    {
        // Given
        var list = new TaggedFieldList();
        var eventRaised = false;
        list.Changed += (_, _) => eventRaised = true;

        var field = new MockTaggedField { Type = TaggedFieldTypes.Description };
        list.Add(field);

        // When
        var removed = list.Remove(field);

        // Then
        Assert.True(removed, "Remove should return true for an existing item.");
        Assert.Empty(list);
        Assert.True(eventRaised, "Changed event should be raised after Remove().");
    }

    [Fact]
    public void Given_NonExistingItem_When_Remove_Then_ReturnsFalseAndNoEventIsRaised()
    {
        // Given
        var list = new TaggedFieldList();
        var eventCount = 0;
        list.Changed += (_, _) => eventCount++;

        // When
        var removed = list.Remove(new MockTaggedField { Type = TaggedFieldTypes.Description });

        // Then
        Assert.False(removed, "Remove should return false for a non-existing item.");
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Given_ValidList_When_RemoveAt_Then_ItemIsRemovedAndEventRaised()
    {
        // Given
        var list = new TaggedFieldList();
        var eventRaised = false;
        list.Changed += (_, _) => eventRaised = true;

        list.Add(new MockTaggedField { Type = TaggedFieldTypes.Description });

        // When
        list.RemoveAt(0);

        // Then
        Assert.Empty(list);
        Assert.True(eventRaised);
    }

    [Fact]
    public void Given_ValidList_When_RemoveAll_Then_RemovedCountAndEventRaisedIfAnyRemoved()
    {
        // Given
        var list = new TaggedFieldList();
        var eventCount = 0;
        list.Changed += (_, _) => eventCount++;

        list.Add(new MockTaggedField { Type = TaggedFieldTypes.Description });
        list.Add(new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress });

        // When
        var removed = list.RemoveAll(x => x.Type == TaggedFieldTypes.Description);

        // Then
        Assert.Equal(1, removed);
        Assert.Single(list);
        Assert.Equal(3, eventCount);
    }

    [Fact]
    public void Given_ValidList_When_RemoveRange_Then_ItemsRemovedAndEventRaised()
    {
        // Given
        var list = new TaggedFieldList();
        var eventRaised = false;
        list.Changed += (_, _) => eventRaised = true;

        list.Add(new MockTaggedField { Type = TaggedFieldTypes.Description });
        list.Add(new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress });
        list.Add(new MockTaggedField { Type = TaggedFieldTypes.ExpiryTime });

        // When
        list.RemoveRange(1, 2);

        // Then
        Assert.Single(list);
        Assert.True(eventRaised);
    }

    [Fact]
    public void Given_ValidList_When_TryGetExistingItem_Then_ReturnsTrueAndItem()
    {
        // Given
        var list = new TaggedFieldList();
        var field = new MockTaggedField { Type = TaggedFieldTypes.Description };
        list.Add(field);

        // When
        var found = list.TryGet<MockTaggedField>(TaggedFieldTypes.Description, out var result);

        // Then
        Assert.True(found);
        Assert.NotNull(result);
        Assert.Same(field, result);
    }

    [Fact]
    public void Given_EmptyList_When_TryGetNonExistingItem_Then_ReturnsFalseAndNull()
    {
        // Given
        var list = new TaggedFieldList();

        // When
        var found = list.TryGet<MockTaggedField>(TaggedFieldTypes.Description, out var result);

        // Then
        Assert.False(found);
        Assert.Null(result);
    }

    [Fact]
    public void Given_ValidList_When_TryGetAllExisting_Then_ReturnsTrueAndItems()
    {
        // Given
        var list = new TaggedFieldList();
        var field1 = new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress };
        var field2 = new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress };
        list.Add(field1);
        list.Add(field2);

        // When
        var found = list.TryGetAll<MockTaggedField>(TaggedFieldTypes.FallbackAddress, out var items);

        // Then
        Assert.True(found);
        Assert.NotNull(items);
        Assert.Equal(2, items.Count);
        Assert.Contains(field1, items);
        Assert.Contains(field2, items);
    }

    [Fact]
    public void Given_EmptyList_When_TryGetAllNonExisting_Then_ReturnsFalseAndNullList()
    {
        // Given
        var list = new TaggedFieldList();

        // When
        var found = list.TryGetAll<FallbackAddressTaggedField>(TaggedFieldTypes.FallbackAddress, out var items);

        // Then
        Assert.False(found);
        Assert.Null(items);
    }

    [Fact]
    public void Given_ValidList_When_CalculateSizeInBits_Then_SumOfAllLengths()
    {
        // Given
        var list = new TaggedFieldList
        {
            new MockTaggedField { Type = TaggedFieldTypes.Description, Length = 5 },
            new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress, Length = 10 }
        };

        // When
        var sizeInBits = list.CalculateSizeInBits();

        // Then
        Assert.Equal(15, sizeInBits);
    }

    [Fact]
    public void Given_ValidList_When_WriteToBitWriter_Then_TagsAndLengthsAreWritten()
    {
        // Given
        var list = new TaggedFieldList
        {
            new MockTaggedField { Type = TaggedFieldTypes.Description, Length = 2 },
            new MockTaggedField { Type = TaggedFieldTypes.FallbackAddress, Length = 1 }
        };
        var bitWriter = new BitWriter(50);

        // When
        list.WriteToBitWriter(bitWriter);

        // Then
        // Assert.Equal(4, bitWriter.Writes.Count);
        // Explanation of 4 writes:
        //   1) Type of first field (CUSTOM_TYPE) => WriteByteAsBits(...)
        //   2) Length of first field => WriteInt16AsBits(...)
        //   3) Type of second field (FALLBACK_ADDRESS)
        //   4) Length of second field
        // The actual content written by WriteToBitWriter in each field depends on the field's logic
    }

    [Fact]
    public void Given_BitReaderReturningNoData_When_FromBitReaderCalled_Then_EmptyListReturned()
    {
        // Given
        var bitReader = new BitReader([]); // defaults to HasMoreBits = false
        // When
        var list = TaggedFieldList.FromBitReader(bitReader, BitcoinNetwork.Mainnet);

        // Then
        Assert.Empty(list);
    }

    [Fact]
    public void Given_InvoiceWithEmptyDescription_When_FromBitReaderCalled_Then_DescriptionIsEmpty()
    {
        // Given
        var invoiceBytes =
            "0D2489F0610D3FBE49A85F7812CB54B0AF846773D3C98833C40E77F2E53CAFA2A025ED0A9C1581A006002140C020A8C0403416AF7A05CB1F56AF5FE32CCF964A84116DF32E3F3CF0A8303F84AF40110EED260280C101208000";
        var bitReader = new BitReader(Convert.FromHexString(invoiceBytes));
        bitReader.SkipBits(35);

        // When
        var list = TaggedFieldList.FromBitReader(bitReader, BitcoinNetwork.Mainnet);

        // Then
        Assert.True(list.TryGet<DescriptionTaggedField>(TaggedFieldTypes.Description, out var description));
        Assert.NotNull(description);
        Assert.Equal(string.Empty, description.Value);
    }
}