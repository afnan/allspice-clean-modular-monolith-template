using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.SharedKernel.UnitTests.Common;

public class EntityEqualityTests
{
    private sealed class Foo : Entity
    {
        public Foo(Guid id) => Id = id;
    }

    private sealed class Bar : Entity
    {
        public Bar(Guid id) => Id = id;
    }

    // Mirrors a downstream entity with a DB-generated integer key: Id is transient (0) until inserted.
    private sealed class IntThing : Entity<int>
    {
        public void AssignId(int id) => Id = id;
    }

    [Fact]
    public void Transient_entities_with_a_default_id_are_not_equal()
    {
        // Two unsaved entities are distinct identities even though both Ids are still default — otherwise
        // every freshly-constructed entity would collide as "equal" before persistence assigns real keys.
        Assert.False(new Foo(Guid.Empty).Equals(new Foo(Guid.Empty)));
        Assert.Equal(2, new HashSet<object> { new Foo(Guid.Empty), new Foo(Guid.Empty) }.Count);
    }

    [Fact]
    public void Hash_code_reflects_the_current_id_not_a_stale_cached_value()
    {
        var thing = new IntThing();              // transient, Id == 0 (e.g. before a DB identity insert)
        var hashWhileTransient = thing.GetHashCode();

        thing.AssignId(42);                       // persistence assigns the real key

        // A cached hash would freeze at the Id==0 value and "lose" the entity in a HashSet after its key
        // is assigned. The hash must follow the current Id.
        Assert.NotEqual(hashWhileTransient, thing.GetHashCode());
    }

    [Fact]
    public void Same_type_same_id_are_equal()
    {
        var id = Guid.NewGuid();

        Assert.True(new Foo(id).Equals(new Foo(id)));
        Assert.Equal(new Foo(id).GetHashCode(), new Foo(id).GetHashCode());
    }

    [Fact]
    public void Same_type_different_id_are_not_equal()
    {
        Assert.False(new Foo(Guid.NewGuid()).Equals(new Foo(Guid.NewGuid())));
    }

    [Fact]
    public void Different_types_with_same_id_are_not_equal()
    {
        var id = Guid.NewGuid();

        // Identity-based equality must also compare the runtime type, otherwise a Foo and a Bar sharing an Id
        // would collide in a HashSet / LINQ Distinct.
        Assert.False(new Foo(id).Equals(new Bar(id)));
        Assert.Equal(2, new HashSet<object> { new Foo(id), new Bar(id) }.Count);
    }
}
