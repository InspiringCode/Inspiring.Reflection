using FluentAssertions;
using Inspiring.BDD;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace Inspiring.Reflection.Tests.Generics {
    public class TypeInferenceTests : FeatureBase {
        public static IEnumerable<object[]> Cases = new TestCaseBuilder(typeof(TypeInferenceTests))
            .Method(nameof(ExpectingT))
                .SucceedsWith<int>().Returns<int>()
            .Method(nameof(ExpectingEnumerableOfT))
                .SucceedsWith<List<int>>().Returns<int>()
            .Method(nameof(ExpectingValueEnumerableOf))
                .SucceedsWith<IStringValueCollection>().Returns<string>()
            .Method(nameof(ExpectedValueOfT))
                .FailsWith<ObjectAndStringValue>().ReturnsEmpty()
            .Method(nameof(ExpectingActionOfListOfT))
                .SucceedsWith<Action<IEnumerable<string>>>().Returns<string>()
            .Method(nameof(ExpectingActionOfValueCollectionOfT))
                .SucceedsWith<Action<TestCollection<IValue<string>>>>().Returns<string>()
            .Method(nameof(ExpectingTwoValuesOfT))
                .SucceedsWith<StringAndIntValue, IValue<int>>().Returns<int>()
                .FailsWith<ObjectAndStringValue, StringAndIntValue>().ReturnsEmpty()
            .Method(nameof(ExpectingNullableOfTAndLongAndObject))
                .SucceedsWith<Nullable<DateTime>, long, string>().Returns<DateTime>()
                .SucceedsWith<Nullable<DateTime>, int, string>().Returns<DateTime>()
                .SucceedsWith<DateTime, long, string>().Returns<DateTime>()
                .FailsWith<Nullable<DateTime>, double, object>().ReturnsEmpty()
            .Method(nameof(ReturnsInt))
                .SucceedsWith<int>().ReturnsEmpty()
                .SucceedsWith<int?>().ReturnsEmpty()
                .SucceedsWith<object>().ReturnsEmpty()
                .FailsWith<long>().ReturnsEmpty()
            .Build();

        [Scenario]
        [MemberData(nameof(Cases))]
        internal void InferGenericArguments(string method, Type[] argumentTypes, bool result, Type[] expected) {
            bool actualResult = default;
            Type[] inferred = default!;

            WHEN["infering type arguments"] = () => actualResult = Infer(method, argumentTypes, out inferred);

            THEN[$"inference {(result ? "succeeds" : "fails")}"] = () => actualResult.Should().Be(result);
            AND["the type arguments are inferred correctly"] = () => inferred.Should().BeEquivalentTo(expected);
        }

        [Scenario]
        internal void ReturnTypeInference(bool result, Type[] inferred) {
            WHEN["passing one more type then parameters"] = () =>
                result = Infer<int, IEnumerable<string>>(nameof(ExpectingSAndReturningListOfR), out inferred);
            THEN["the result is true"] = () => result.Should().BeTrue();
            AND["all return type is used for inference"] = () =>
                inferred.Should().BeEquivalentTo(new[] { typeof(string), typeof(int) });
        }

        [Scenario]
        internal void InferFromConstraints(bool result, Type[] inferred) {
            WHEN["one generic parameter is unbound"] = () => result = Infer(
                nameof(HasUnboundGenericParameter), 
                new[] { typeof(List<string>) }, 
                out inferred, 
                inferFromConstraints: true);
            THEN["the result is true"] = () => result.Should().BeTrue();
            AND["and the unbound parameter is inferred via the constraints"] = () =>
                inferred.Should().BeEquivalentTo(new[] { typeof(List<string>), typeof(string) });

            WHEN["two generic parameters are unbound"] = () => result = Infer(
                nameof(HasTwoUnboundGenericParameter),
                new[] { typeof(List<StringValue>) },
                out inferred,
                inferFromConstraints: true);
            THEN["the result is true"] = () => result.Should().BeTrue();
            AND["and the unbound parameter is inferred via the constraints"] = () =>
                inferred.Should().BeEquivalentTo(new[] { typeof(List<StringValue>), typeof(StringValue), typeof(string) });

            WHEN["one generic parameter is unbound"] = () => result = Infer(
                nameof(HasUnresolvableGenericParameters),
                new[] { typeof(List<StringValue>) },
                out inferred,
                inferFromConstraints: true);
            THEN["the result is false"] = () => result.Should().BeFalse();
        }

        [Scenario]
        internal void ConstraintChecking(bool result, Type[] inferred) {
            WHEN["the constraints are satisfied"] = () =>
                result = Infer<Cat, IAnimal>(nameof(ExpectingTAndBaseOfT), out inferred);
            THEN["inference succeeds"] = () => result.Should().BeTrue();
            AND["all types are inferred"] = () =>
                inferred.Should().BeEquivalentTo(new[] { typeof(Cat), typeof(IAnimal) });

            WHEN["the constraints are not satisfied"] = () =>
                result = Infer<IAnimal, Cat>(nameof(ExpectingTAndBaseOfT), out inferred);
            THEN["inference fails"] = () => result.Should().BeFalse();

            WHEN["dontCheckConstraints is set to true"] = () =>
                result = Infer<IAnimal, Cat>(nameof(ExpectingTAndBaseOfT), out inferred, dontCheckGenericConstraints: true);
            THEN["the constraints are ignored"] = () => result.Should().BeTrue();
            AND["all types are inferred"] = () =>
                inferred.Should().BeEquivalentTo(new[] { typeof(IAnimal), typeof(Cat) });

            WHEN["a method expects some Xyz<ConcreteType, T> (in that order)"] = () => Infer(
                nameof(ExpectTupleOfStringValueAndT),
                new[] { typeof(Tuple<StringValue, int>) },
                out inferred);
            THEN["the generic parameter is inferred"] = () => inferred.Should().BeEquivalentTo(new[] { typeof(int) });
        }


        public List<R> ExpectingSAndReturningListOfR<R, S>(S arg)
            => throw new InvalidOperationException();

        public void ExpectingT<T>(T arg) { }

        public void ExpectingEnumerableOfT<T>(IEnumerable<T> arg) { }

        public void ExpectedValueOfT<T>(IValue<T> arg) { }

        public void ExpectingTwoValuesOfT<T>(IValue<T> x, IValue<T> y) { }

        public void ExpectingValueEnumerableOf<T>(IEnumerable<IValue<T>> arg) { }

        public void ExpectingActionOfListOfT<T>(Action<List<T>> arg) { }

        public void ExpectingActionOfValueCollectionOfT<T>(Action<ValueCollection<T>> arg) { }

        public void ExpectingNullableOfTAndLongAndObject<T>(Nullable<T> arg1, long arg2, object arg3) where T : struct { }

        public int ReturnsInt() => 0;

        public void ExpectingTAndBaseOfT<T, TBase>(T arg1, TBase arg2) where T : TBase { }

        public void ExpectTupleOfStringValueAndT<T>(Tuple<StringValue, T> arg) { }

        public void HasUnboundGenericParameter<TCollection, TItem>(TCollection items) where TCollection : IEnumerable<TItem> { }

        public void HasTwoUnboundGenericParameter<TCollection, TItem, TValueType>(TCollection items)
            where TCollection : IEnumerable<TItem>, IReadOnlyCollection<object>
            where TItem : IHasValue, IValue<TValueType> { }

        public void HasUnresolvableGenericParameters<TCollection, TItem, TValueType, TResolvable>(TCollection item)
            where TCollection : IEnumerable<TItem>, IReadOnlyCollection<object>
            where TItem : IHasValue, IValue<TValueType> { }

        public interface IHasValue { }

        public interface IValue<out T> { }

        public interface IStringValueCollection : ICollection<StringValue> { }

        public class StringValue : IValue<string>, IHasValue { }

        public class ObjectAndStringValue : IValue<string>, IValue<object> { }

        public class StringAndIntValue : IValue<string>, IValue<int> { }

        public class ValueCollection<T> : TestCollection<IValue<T>> { }

        public class TestCollection<T> { }

        public interface IAnimal { }

        public class Cat : IAnimal { }

        private bool Infer<T1, T2>(string method, out Type[] inferred, bool dontCheckGenericConstraints = false)
            => Infer(method, new[] { typeof(T1), typeof(T2) }, out inferred, dontCheckGenericConstraints);

        private bool Infer(
            string method, 
            Type[] argumentTypes, 
            out Type[] inferred, 
            bool dontCheckGenericConstraints = false,
            bool inferFromConstraints = false
        ) => GetType().GetMethod(method)!.TryInferTypes(argumentTypes, out inferred, dontCheckGenericConstraints, inferFromConstraints);
    }
}
