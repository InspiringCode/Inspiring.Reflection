using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Inspiring.Reflection.Tests.Generics {
    internal class TestCaseBuilder {
        private readonly Type _testClass;
        private readonly List<List<object>> _data = new();
        private object? _currentMember;


        public TestCaseBuilder(Type testClass) {
            _testClass = testClass;
        }

        public TestCaseBuilder Type(Type type) {
            _currentMember = type;
            return this;
        }

        public TestCaseBuilder Method(string name) {
            _currentMember = name;
            //_currentMember = _testClass.GetMethod(
            //    name,
            //    BindingFlags.NonPublic |
            //        BindingFlags.Public |
            //        BindingFlags.Instance |
            //        BindingFlags.Static);

            return this;
        }

        public TestCaseBuilder SucceedsWith<TArg>()
            => Add(true, typeof(TArg));
        public TestCaseBuilder SucceedsWith<TArg1, TArg2>()
            => Add(true, typeof(TArg1), typeof(TArg2));
        public TestCaseBuilder SucceedsWith<TArg1, TArg2, TArg3>()
            => Add(true, typeof(TArg1), typeof(TArg2), typeof(TArg3));
        public TestCaseBuilder FailsWith<TArg>()
            => Add(false, typeof(TArg));

        public TestCaseBuilder FailsWith<TArg1, TArg2>()
            => Add(false, typeof(TArg1), typeof(TArg2));

        public TestCaseBuilder FailsWith<TArg1, TArg2, TArg3>()
            => Add(false, typeof(TArg1), typeof(TArg2), typeof(TArg3));

        public TestCaseBuilder ReturnsEmpty() {
            _data[^1].Add(Array.Empty<Type>());
            return this;
        }

        public TestCaseBuilder Returns<TArg>() {
            _data[^1].Add(new[] { typeof(TArg) });
            return this;
        }

        public IEnumerable<object[]> Build() => _data.Select(x => x.ToArray());

        private TestCaseBuilder Add(bool result, params Type[] args) {
            _data.Add(new List<object> {
                _currentMember ?? throw new InvalidOperationException(),
                args,
                result
            });

            return this;
        }
    }

    //internal class TestCase : IXunitSerializable {
    //    public Type Type = null!;
    //    public Type[] Args = null!;
    //    public bool Result;

    //    public void Deserialize(IXunitSerializationInfo info) {
    //        Type = info.GetValue<Type>(nameof(Type));
    //        Args = info.GetValue<Type[]>(nameof(Args));
    //        Result = info.GetValue<bool>(nameof(Result));
    //    }

    //    public void Serialize(IXunitSerializationInfo info) {
    //        info.AddValue(nameof(Type), Type);
    //        info.AddValue(nameof(Args), Args);
    //        info.AddValue(nameof(Result), Result);
    //    }

    //    public override string ToString()
    //        => $"{Type.Name}<{string.Join(", ", Args.Select(x => x.Name))}>";
    //}
}
