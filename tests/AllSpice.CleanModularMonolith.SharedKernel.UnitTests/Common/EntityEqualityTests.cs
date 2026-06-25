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
