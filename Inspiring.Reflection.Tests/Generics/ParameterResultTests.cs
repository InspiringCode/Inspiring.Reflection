using FluentAssertions;
using Inspiring.BDD;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inspiring.Reflection.Tests.Generics {
    public class ParameterResultTests : FeatureBase {
        [Scenario]
        internal void Fixing(Fixture f, Type? t) {
            GIVEN["no common subtype in upper bounds"] = () =>
                f = new Fixture().Upper<IAnimal>().Upper<IWarmblooded>();
            AND["a common basetype in lower bounds (assignable to upper bounds)"] = () =>
                f.Lower<ILion>().Lower<ICat>().Lower<ITiger>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeTrue();
            THEN["the common basetype of the lower bounds is selected"] = () => t.Should().Be<ICat>();

            GIVEN["a common subtype in upper bounds (assignable from lower bounds)"] = () =>
                f = new Fixture().Upper<IAnimal>().Upper<IWarmblooded>().Upper<IMammal>();
            AND["no common basetype in lower bounds"] = () =>
                f.Lower<ILion>().Lower<ITiger>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeTrue();
            THEN["the common subtype of the upper bounds is selected"] = () => t.Should().Be<IMammal>();

            GIVEN["a common subtype in upper bounds and no lower bounds"] = () =>
                f = new Fixture().Upper<IAnimal>().Upper<IWarmblooded>().Upper<IMammal>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeTrue();
            THEN["the upper bound is selected"] = () => t.Should().Be<IMammal>();

            GIVEN["no upper bounds and a common basetype in lower bounds"] = () =>
                f = new Fixture().Lower<ILion>().Lower<ICat>().Lower<ITiger>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeTrue();
            THEN["the lower bound is selected"] = () => t.Should().Be<ICat>();

            GIVEN["a common subtype in upper bounds"] = () => f = new Fixture().Upper<IDog>();
            AND["a common basetype in lower bounds (not assignable to upper bound)"] = () =>
                f.Lower<ICat>().Lower<ITiger>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeFalse();
            THEN["the inference fails"] = () => t.Should().BeNull();

            GIVEN["exactly one exact bound"] = () => f = new Fixture().Exact<Mammal>();
            AND["compatible lower and upper bounds"] = () => f.Upper<IAnimal>().Lower<Tiger>();
            WHEN["fixing"] = () => f.Fix(out t).Should().BeTrue();
            THEN["the exact bound is selected"] = () => t.Should().Be<Mammal>();

            GIVEN["exactly one exact bound"] = () => f = new Fixture().Exact<Mammal>();
            AND["incompatible lower or upper bounds"] = () => f.Lower<ITiger>();
            THEN["inference fails"] = () => f.Fix(out t).Should().BeFalse();

            GIVEN["two exact bounds"] = () => f = new Fixture().Exact<Mammal>().Exact<Tiger>();
            THEN["inference fails"] = () => f.Fix(out t).Should().BeFalse();
        }

        private interface IAnimal { }

        private interface IWarmblooded { }

        private interface IMammal : IAnimal, IWarmblooded { }

        private interface ICat : IMammal { }

        private interface IDog : IMammal { }

        private interface ILion : ICat { }

        private interface ITiger : ICat { }

        private class Mammal : IMammal { }

        private class Tiger : Mammal, ITiger { }

        internal class Fixture {
            private TypeExtensions.ParameterBounds _bounds;
            
            public Fixture Lower<T>() {
                _bounds.Lower.Add(typeof(T));
                return this;
            }
            
            public Fixture Exact<T>() {
                _bounds.Exact.Add(typeof(T));
                return this;
            }


            public Fixture Upper<T>() {
                _bounds.Upper.Add(typeof(T));
                return this;
            }

            internal bool Fix(out Type? inferredType) {
                bool result = _bounds.TryFixType();
                inferredType = _bounds.InferredType;
                return result;
            }
        }
    }
}
