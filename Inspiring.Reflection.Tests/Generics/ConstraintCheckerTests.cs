using System.Collections;
using System.Reflection;

namespace Inspiring.Reflection.Tests.Generics {
    public partial class ConstraintCheckerTests {
        public static IEnumerable<object[]> Cases = new TestCaseBuilder(typeof(ConstraintCheckerTests))
            .Type(typeof(EnumerableConstraint<>))
                .SucceedsWith<int[]>()
                .FailsWith<Type>()
            .Type(typeof(ListOfConstraint<,>))
                .SucceedsWith<List<string>, string>()
                .FailsWith<List<ISub>, IBase>()
            .Type(typeof(Arg1ImplementsArg2<,>))
                .SucceedsWith<List<int>, IEnumerable>()
                .FailsWith<List<int>, string>()
            .Type(typeof(Circular<,>))
                .SucceedsWith<A<string>, A<string>>()
                .FailsWith<A<string>, A<int>>()
            .Type(typeof(IMessage<,>))
                .SucceedsWith<M1, string>()
                .FailsWith<M1, int>()

            .Type(typeof(NestedConstraint<,>))
                .SucceedsWith<A<string>, B<A<string>>>()
                .FailsWith<A<object>, B<A<string>>>()
            .Build();

        public static IEnumerable<object[]> CovarianceCases = new TestCaseBuilder(typeof(ConstraintCheckerTests))
            .Type(typeof(TypeWithGenericConstraint<,>))
                .SucceedsWith<Func<int>, Func<int>>()
                .SucceedsWith<Func<Base>, Func<Sub>>()
                .SucceedsWith<Func<Base<string>>, Func<Sub<string>>>()
                .SucceedsWith<Func<IBase>, Func<ISub>>()
                .SucceedsWith<Func<Base[]>, Func<Sub[]>>()
                .SucceedsWith<Func<IBase<Base>[]>, Func<ISub<Sub>[]>>()
                .FailsWith<Func<ISub>, Func<IBase>>()
                .FailsWith<Func<Base<object>>, Func<Sub<string>>>()
                .FailsWith<Func<Sub[]>, Func<Base[]>>()
            .Type(typeof(EnumerableOfConstraint<,>))
                .SucceedsWith<List<string>, string>()
                .SucceedsWith<List<ISub>, IBase>()
                .FailsWith<List<IBase>, ISub>()
            .Build();

        public static IEnumerable<object[]> ContravarianceCases = new TestCaseBuilder(typeof(ConstraintCheckerTests))
            .Type(typeof(TypeWithGenericConstraint<,>))
                .SucceedsWith<IAction<int>, IAction<int>>()
                .SucceedsWith<IAction<ISub>, IAction<IBase>>()
                .SucceedsWith<Action<Sub>, Action<Base>>()
                .SucceedsWith<Action<Sub<string>>, Action<Base<string>>>()
                .FailsWith<IAction<IBase>, IAction<ISub>>()
            .Type(typeof(TypeWithGenericConstraint<,>))
                .SucceedsWith<Action<int>, Action<int>>()
                .SucceedsWith<Action<ISub>, Action<IBase>>()
                .FailsWith<Action<IBase>, Action<ISub>>()
            .Build();

        public static IEnumerable<object[]> MethodCases = new TestCaseBuilder(typeof(ConstraintCheckerTests))
            .Method(nameof(Process))
                .SucceedsWith<M1, string>()
                .FailsWith<M1, object>()
            .Build();

        [Scenario]
        [MemberData(nameof(Cases))]
        internal void Satisfies(Type type, Type[] args, bool result) {
            THEN[$"checking the type should {(result ? "succeed" : "fail")}"] = () =>
                type.SatisfiesGenericConstraints(args).Should().Be(result);
        }

        [Scenario]
        [MemberData(nameof(ContravarianceCases))]
        internal void Contravariance(Type type, Type[] args, bool result) {
            THEN[$"checking the type should {(result ? "succeed" : "fail")}"] = () =>
                type.SatisfiesGenericConstraints(args).Should().Be(result);
        }

        [Scenario]
        [MemberData(nameof(CovarianceCases))]
        internal void Covariance(Type type, Type[] args, bool result) {
            THEN[$"checking the type should {(result ? "succeed" : "fail")}"] = () => type.SatisfiesGenericConstraints(args).Should().Be(result);
        }


        [Scenario]
        [MemberData(nameof(MethodCases))]
        internal void Methods(string method, Type[] args, bool result) {
            THEN[$"checking the type should {(result ? "succeed" : "fail")}"] = () =>
                GetType().GetMethod(method)!.SatisfiesGenericConstraints(args).Should().Be(result);
        }


        public void Process<M, R>() where M : IMessage<M, R> { }

        public class TypeWithGenericConstraint<TConstraint, T> where T : TConstraint { }

        public interface IAction<in T> { }

        public interface IFunc<out T> { }

        public interface IBase { }

        public interface ISub : IBase { }

        public interface IBase<out T> { }

        public interface ISub<out T> : IBase<T> { }

        private class Base { }

        private class Sub : Base { }

        private class Base<T> { }

        private class Sub<T> : Base<T> { }

        private class EnumerableConstraint<T> where T : IEnumerable { }

        private class EnumerableOfConstraint<T, TItem> where T : IEnumerable<TItem> { }

        private class ListOfConstraint<T, TItem> where T : IList<TItem> { }

        private class Arg1ImplementsArg2<S, T> where S : T { }

        private class Circular<S, T> where S : IA<T> where T : IA<S> { }

        private class NestedConstraint<S, T> where S : IA<T> where T : IB<S, IC<S, T>> { }

        private interface IA<T> { }

        private interface IB<U, V> { }

        private interface IC<X, Y> { }

        public class B<S> : IB<S, IC<S, B<S>>> { }

        private class A<T> : IA<A<T>>, IA<B<A<T>>> { }
        public interface IMessage<M, R> where M : IMessage<M, R> { }

        private class M1 : IMessage<M1, string> { }
    }

    public partial class ConstraintCheckerTests : FeatureBase {
        [Scenario]
        internal void TypeParameterConstraints(Type t) {
            GIVEN["a value type constraint"] = () => t = typeof(ValueTypeConstraint<>);
            THEN["checking with a value type succeeds"] = () => Check<DateTime>(t).Should().BeTrue();
            THEN["checking iwth an enum succeeds"] = () => Check<MemberTypes>(t).Should().BeTrue();
            THEN["checking with an interface fails"] = () => Check<IEnumerable>(t).Should().BeFalse();
            THEN["checking with an nullable value type fails"] = () => Check<Nullable<int>>(t).Should().BeFalse();

            GIVEN["a class constraint"] = () => t = typeof(ClassConstraint<>);
            THEN["checking with a class type succeeds"] = () => Check<string>(t).Should().BeTrue();
            THEN["checking with an interface succeeds"] = () => Check<IEnumerable>(t).Should().BeTrue();
            THEN["checking with a value type fails"] = () => Check<int>(t).Should().BeFalse();

            GIVEN["a new constaint"] = () => t = typeof(DefaultConstructorConstraint<>);
            THEN["checking with a class with parameterless ctor succeeds"] = () => Check<object>(t).Should().BeTrue();
            THEN["checking with a value type succeeds"] = () => Check<double>(t).Should().BeTrue();
            THEN["checking with a class without parameterless ctor fails"] = () => Check<ClassWithoutDefaultCtor>(t).Should().BeFalse();
            THEN["checking with an abstract class fails"] = () => Check<AbstractClass>(t).Should().BeFalse();
            THEN["checking with an interface fails"] = () => Check<IEnumerable>(t).Should().BeFalse();
        }

        private static bool Check<TArgument>(Type t) => t.SatisfiesGenericConstraints(typeof(TArgument));


        private static bool Check<TArg1, TArg2>(Type t) => t.SatisfiesGenericConstraints(typeof(TArg1), typeof(TArg2));


        private class ValueTypeConstraint<T> where T : struct { }

        private class ClassConstraint<T> where T : class { }

        private class DefaultConstructorConstraint<T> where T : new() { }

        private class ClassWithoutDefaultCtor {
            public ClassWithoutDefaultCtor(string value) { }
        }

        private abstract class AbstractClass {
            public AbstractClass() { } // Not defined by default
        }
    }
}
