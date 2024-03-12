using FluentAssertions;
using Inspiring.BDD;
using System.Reflection;
using static Inspiring.Reflection.TypeExtensions;

namespace Inspiring.Reflection.Tests.Generics {
    public class InferredBoundsTests : FeatureBase {
        [Scenario]
        internal void GeneralCases(MethodFixture f) {
            WHEN["a method expects an input parameter of type T"] = () => f = new MethodFixture(nameof(ExpectsT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<string>()
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a method expects a ref parameter of type T"] = () => f = new MethodFixture(nameof(ExpectsRefT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<string>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(string) } });

            WHEN["a method expects a out parameter of type T"] = () => f = new MethodFixture(nameof(ExpectsOutT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<string>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(string) } });

            WHEN["one more argument than method parameters is passed"] = () => f = new MethodFixture(nameof(ReturnsTAndExpectsObject));
            THEN["an upper bound is inferred"] = () => f
                .InferFrom<string>(additionalArgumentTypes: new[] { typeof(IAnimal) })
                .Should().BeEquivalentTo(new Bounds { Upper = { typeof(IAnimal) } });
        }

        [Scenario]
        internal void GenericInterfaceOrTypeImplementations(MethodFixture f) {
            WHEN["a method expects an invariant List<T> and a List<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsListOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<List<string>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(string) } });

            WHEN["a method expects a covariant IEnumerable<T> and a List<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsIEnumerableOfT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<List<string>>()
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a covariant argument implements the required interface multiple times"] = () => f = new MethodFixture(nameof(ExpectsIAnimalOfT));
            THEN["no bounds are inferred"] = () => f
                .InferFrom<Tiger>()
                .Should().BeEquivalentTo(new Bounds());

            WHEN["a parameter is contravariant"] = () => f = new MethodFixture(nameof(ExpectsActionOfListOfT));
            THEN["the parameter is checked if it implements the argument interface"] = () => f
                .InferFrom<Action<ICollection<string>>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(string) } });

            WHEN["a contravariant parameter implements the argument interface multiple times"] = () => f = new MethodFixture(nameof(ExpectsActionOfIAsynResultOfT));
            THEN["no bounds are inferred"] = () => {
                f.InferFrom<Action<IComparable<Task<string>>>>().Should().BeEquivalentTo(new Bounds());
                f.InferFrom<Action<IComparable<ValueTask<string>>>>().Should().BeEquivalentTo(new Bounds());
            };

            WHEN["a covariant parameter is nested in a contravariant parameter"] = () => f = new MethodFixture(nameof(ExpectsActionOfListOfT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<Action<IEnumerable<string>>>()
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a covariant parameter is not a reference type"] = () => f = new MethodFixture(nameof(ExpectsIEnumerableOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<IEnumerable<int>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["some generic parameters of a parameter type are concrete types (that are compatible)"] = () => f = new MethodFixture(nameof(ExpectsActionOfTAndCat));
            THEN["the type parameters are still infered"] = () => f
                .InferFrom<Action<int, IAnimal>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });
        }

        [Scenario]
        internal void NullableTypes(MethodFixture f) {
            // We comply to the MethodInfo.Invoke behavior here and not to the C# spec
            WHEN["a method expects a Nullable<T> and Nullable<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsNullableOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<Nullable<int>>()
                .Should().BeEquivalentTo(new Bounds { Exact  = { typeof(int) } });

            WHEN["a method expects a Nullable<T> and T is passed"] = () => f = new MethodFixture(nameof(ExpectsNullableOfT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<int>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["a contravariant type expects a Nullable<T> and Nullable<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsFuncOfNullableOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<Func<Nullable<int>>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["a covariant type expects a Nullable<T> and T is passed"] = () => f = new MethodFixture(nameof(ExpectsFuncOfNullableOfT));
            THEN["inference fails"] = () => f
                .InferFrom<Func<int>>().Success.Should().BeFalse();

            WHEN["a contravariant type expects a Nullable<T> and Nullable<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsActionOfNullableOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<Action<Nullable<int>>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["a contravariant type expects a T and Nullable<T> is passed"] = () => f = new MethodFixture(nameof(ExpectsActionOfInt));
            THEN["inference fails"] = () => f
                .InferFrom<Action<Nullable<int>>>().Success.Should().BeFalse();
        }


        [Scenario]
        internal void Arrays(MethodFixture f) {
            WHEN["a method expects a T[] and T is a reference type"] = () => f = new MethodFixture(nameof(ExpectsArrayOfT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<string[]>()
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a method expects a T[] and T is a value type"] = () => f = new MethodFixture(nameof(ExpectsArrayOfT));
            THEN["an exact bound is inferred"] = () => f
                .InferFrom<int[]>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["a method expects a collection interface and an array is passed"] = () => f = new MethodFixture(nameof(ExpectsIListOfT));
            THEN["a lower bound is inferred"] = () => f
                .InferFrom<string[]>()
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a method expects an array contravariantly and a collection interface is passed"] = () => f = new MethodFixture(nameof(ExpectActionOfArrayOfT));
            THEN["an upper bound is inferred"] = () => f
                .InferFrom<Action<ICollection<string>>>()
                .Should().BeEquivalentTo(new Bounds { Upper = { typeof(string) } });
        }

        [Scenario]
        internal void NonGenericParameters(MethodFixture f) {
            WHEN["a method expects a generic parameter and non-generic parameter (that is compatible)"] = () => f = new MethodFixture(nameof(ExpectsTAndLong));
            THEN["the generic parameters are inferred"] = () => f
                .InferFrom<string>(additionalArgumentTypes: new[] { typeof(int) })
                .Should().BeEquivalentTo(new Bounds { Lower = { typeof(string) } });

            WHEN["a method expects a generic parameter and non-generic parameter (that is not compatible)"] = () => f = new MethodFixture(nameof(ExpectsTAndLong));
            THEN["inference fails"] = () => f
                .InferFrom<string>(additionalArgumentTypes: new[] { typeof(DateTime) })
                .Should().BeEquivalentTo(new Bounds { Success = false });

            WHEN["some generic parameters of an interface are concrete types (that are compatible)"] = () => f = new MethodFixture(nameof(ExpectsActionOfTAndCat));
            THEN["the generic parameters are inferred"] = () => f
                .InferFrom<Action<int, IAnimal>>()
                .Should().BeEquivalentTo(new Bounds { Exact = { typeof(int) } });

            WHEN["some generic parameters of an interface are concrete types (that are not compatible)"] = () => f = new MethodFixture(nameof(ExpectsActionOfTAndCat));
            THEN["the generic parameters are inferred"] = () => f
                .InferFrom<Action<int, Tiger>>().Success.Should().BeFalse();
        }

        private void ExpectsT<T>(T arg) { }

        private void ExpectsRefT<T>(ref T arg) { }

        private void ExpectsOutT<T>(out T arg) { arg = default!; }

        private T ReturnsTAndExpectsObject<T>(object arg) => default!;

        private void ExpectsListOfT<T>(List<T> arg) { }

        private void ExpectsIEnumerableOfT<T>(IEnumerable<T> arg) { }

        private void ExpectsIAnimalOfT<T>(IAnimal<T> arg) { }

        private void ExpectsActionOfIAsynResultOfT<T>(Action<IAsyncResult<T>> arg) { }

        private void ExpectsActionOfListOfT<T>(Action<List<T>> arg) { }


        private void ExpectsNullableOfT<T>(Nullable<T> arg) where T : struct { }

        private void ExpectsFuncOfNullableOfT<T>(Func<Nullable<T>> arg) where T : struct { }

        private void ExpectsActionOfNullableOfT<T>(Action<Nullable<T>> arg) where T : struct { }

        private void ExpectsActionOfInt(Action<int> arg) { }


        private void ExpectsArrayOfT<T>(T[] arg) { }

        private void ExpectsIListOfT<T>(IList<T> arg) { }

        private void ExpectActionOfArrayOfT<T>(Action<T[]> arg) { }


        private void ExpectsTAndLong<T>(T arg1, long arg2) { }

        private void ExpectsActionOfTAndCat<T>(Action<T, Cat> arg) { }


        private interface IAnimal { }

        private interface IAnimal<T> : IAnimal { }

        private class Cat : IAnimal<Cat> { }

        private class Tiger : Cat, IAnimal<Tiger> { }

        private interface IAsyncResult<T> : IComparable<Task<T>>, IComparable<ValueTask<T>> { }


        internal class Bounds {
            public bool Success { get; init; } = true;

            public List<Type> Upper { get; init; } = new();
            public List<Type> Exact { get; init; } = new();
            public List<Type> Lower { get; init; } = new();
        }

        internal class MethodFixture {
            private readonly MethodInfo _method;
            public MethodFixture(string methodName) {
                _method =
                    typeof(InferredBoundsTests).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance) ??
                    throw new ArgumentException();
            }

            public Bounds InferFrom<T>(params Type[] additionalArgumentTypes) {
                Type[] argumentTypes = new[] { typeof(T) }
                    .Concat(additionalArgumentTypes)
                    .ToArray();

                bool success = TypeExtensions.TryInferBounds(_method, argumentTypes, out InferredBounds bounds);

                if (bounds.Bounds == null)
                    return new Bounds { Success = success };

                ParameterBounds pb = bounds.Bounds[0];
                return new Bounds {
                    Success = success,
                    Lower = pb.Lower.AsList(),
                    Exact = pb.Exact.AsList(),
                    Upper = pb.Upper.AsList()
                };
            }
        }
    }
}
