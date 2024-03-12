using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inspiring.Reflection {

#if INSPIRING_REFLECTION
    public
#else
    internal 
#endif
    static partial class TypeExtensions {
        private static readonly Func<Type, Type, bool> CanValueSpecialCast = CreateCanValueSpecialCastDelegate();
        private static readonly Type NullableType = typeof(Nullable<>);

        public static bool TryInferTypes(
            this MethodInfo method,
            Type[] argumentTypes,
            out Type[] genericArgs,
            bool dontCheckGenericConstraints = false,
            bool inferFromConstraints = false
        ) {
            genericArgs = Type.EmptyTypes;

            bool allInferred =
                TryInferBounds(method, argumentTypes, out InferredBounds bounds) &&
                bounds.TryInferGenericParameters(out genericArgs, breakEarly: !inferFromConstraints);

            if (!allInferred && inferFromConstraints)
                allInferred = bounds.TryInferParametersFromConstraints(out genericArgs);


            return allInferred && (dontCheckGenericConstraints || method.SatisfiesGenericConstraints(genericArgs));
        }


#if INSPIRING_REFLECTION
        internal
#endif
        static bool TryInferBounds(this MethodInfo method, Type[] argumentTypes, out InferredBounds bounds) {
            bounds = new InferredBounds();
            ParameterInfo[] @params = method.GetParameters();

            for (int i = 0; i < @params.Length; i++) {
                if (typesAreIncompatible(argumentTypes[i], @params[i].ParameterType))
                    return false;
            }

            bool returnTypeInompatible =
                argumentTypes.Length > @params.Length &&
                typesAreIncompatible(source: method.ReturnType, target: argumentTypes[@params.Length]);

            if (returnTypeInompatible)
                return false;

            bounds = new InferredBounds(method.GetGenericArguments());

            for (int i = 0; i < @params.Length; i++) {
                Type param = @params[i].ParameterType;

                if (param.ContainsGenericParameters) {
                    Type arg = argumentTypes[i];
                    GenericParameterAttributes paramVariance = GenericParameterAttributes.Covariant;

                    // We comply to the MethodInfo.Invoke behavior and not to the exact C# specification:
                    // Invoking a Foo(Nullable<long>) with int or Nullable<int> throws an exception!
                    if (AreEqual(param, NullableType) && !AreEqual(arg, NullableType)) {
                        param = param.GetGenericArguments()[0];
                        paramVariance = GenericParameterAttributes.None;
                    }

                    if (param.IsByRef) {
                        param = param.GetElementType();
                        paramVariance = GenericParameterAttributes.None;
                    }

                    if (!bounds.TryInferGenericParameterBounds(arg, param, paramVariance))
                        return false;
                }
            }

            if (argumentTypes.Length > @params.Length) {
                GenericParameterAttributes variance = GenericParameterAttributes.Contravariant;

                Type arg = argumentTypes[@params.Length];
                if (AreEqual(arg, NullableType) && !AreEqual(method.ReturnType, NullableType)) {
                    arg = arg.GetGenericArguments()[0];
                    variance = GenericParameterAttributes.None;
                }

                return bounds.TryInferGenericParameterBounds(
                    source: arg,
                    target: method.ReturnType,
                    variance: variance);
            }

            return true;

            static bool typesAreIncompatible(Type source, Type target) {
                if (source.ContainsGenericParameters || target.ContainsGenericParameters) return false;

                bool compatible =
                    target.IsAssignableFrom(source) || (
                        (source.IsPointer || source.IsEnum || source.IsPrimitive) &&
                        CanValueSpecialCast(source, target));

                return !compatible;
            }
        }


        private static bool AreEqual(Type x, Type y)
            => x.Name == y.Name && x.Namespace == y.Namespace;

#if INSPIRING_REFLECTION
        internal
#endif
        readonly struct InferredBounds {
            private static readonly string[] ArrayInterfaceNames = {
                typeof(IEnumerable<>).Name,
                typeof(IReadOnlyCollection<>).Name,
                typeof(IReadOnlyList<>).Name,
                typeof(ICollection<>).Name,
                typeof(IList<>).Name
            };

            private readonly Type[] _genericParameters;
            private readonly ParameterBounds[] _bounds;

            public ParameterBounds[] Bounds => _bounds;

            public InferredBounds(Type[] genericParameters) {
                _genericParameters = genericParameters;
                _bounds = new ParameterBounds[genericParameters.Length];
            }

            public bool TryInferGenericParameterBounds(Type source, Type target, GenericParameterAttributes variance) {
                if (target.IsGenericParameter) {
                    Add(target, source, variance);
                    return true;
                }

                if (HandleArrayTypes(source, target, variance, out bool result))
                    return result;

                bool implementsUniquely = true;
                bool implementsTypeOrInterface = variance switch {
                    GenericParameterAttributes.Covariant => FindImplementation(source, target, ref source, out implementsUniquely),
                    GenericParameterAttributes.Contravariant => FindImplementation(target, source, ref target, out implementsUniquely),
                    _ => AreEqual(source, target)
                };

                if (!implementsTypeOrInterface)
                    return false;

                if (!implementsUniquely)
                    return true;

                return CheckImplementation(source, target, variance);

            }

            public bool TryInferGenericParameters(out Type[] genericArguments, bool breakEarly) {
                bool success = true;

                for (int i = 0; i < _bounds.Length; i++) {
                    if (!_bounds[i].TryFixType()) {
                        success = false;
                        if (breakEarly) break;
                    }
                }

                genericArguments = success ?
                    Array.ConvertAll(_bounds, pb => pb.InferredType!) :
                    Type.EmptyTypes;

                return success;
            }


            public bool TryInferParametersFromConstraints(out Type[] genericArguments) {
                genericArguments = Type.EmptyTypes;
                int previousUnresolved;
                int unresolved = Bounds.Count(b => b.InferredType == null);

                do {
                    previousUnresolved = unresolved;

                    foreach (Type genericParameter in _genericParameters) {
                        if (IsFixed(genericParameter, out Type? genericArgument)) {
                            if (!InferBoundsFromConstraint(genericParameter, genericArgument))
                                return false;
                        }
                    }

                    if (TryInferGenericParameters(out genericArguments, breakEarly: false))
                        return true;

                    unresolved = Bounds.Count(b => b.InferredType == null);
                } while (unresolved < previousUnresolved);

                return unresolved == 0;
            }

            private bool InferBoundsFromConstraint(Type genericParameter, Type genericArgument) {
                foreach (Type constraint in genericParameter.GetGenericParameterConstraints()) {
                    bool constraintViolated = !TryInferGenericParameterBounds(
                        source: genericArgument,
                        target: constraint,
                        GenericParameterAttributes.Covariant);

                    if (constraintViolated) return false;
                }

                return true;
            }

            private bool IsFixed(Type genericParameter, [NotNullWhen(true)] out Type? inferredType) {
                int i = Array.IndexOf(_genericParameters, genericParameter);
                inferredType = _bounds[i].InferredType;
                return inferredType != null;
            }

            private bool FindImplementation(Type type, Type @interface, ref Type implementation, out bool isUnique) {
                bool found = false;
                isUnique = true;

                for (Type @base = type; @base != null; @base = @base.BaseType) {
                    if (AreEqual(@base, @interface)) {
                        implementation = @base;
                        return true;
                    }
                }

                foreach (Type i in type.GetInterfaces()) {
                    if (AreEqual(i, @interface)) {
                        if (found) {
                            isUnique = false;
                            return true;
                        }

                        implementation = i;
                        found = true;
                    }
                }

                return found;
            }

            private bool CheckImplementation(Type source, Type target, GenericParameterAttributes variance) {
                Type[] sourceTypes = source.GetGenericArguments();
                Type[] targetTypes = target.GetGenericArguments();
                Type[]? genericParamsCache = null;

                for (int i = 0; i < sourceTypes.Length; i++) {
                    Type s = sourceTypes[i];
                    Type t = targetTypes[i];

                    GenericParameterAttributes parameterVariance = variance == GenericParameterAttributes.None || s.IsValueType ?
                        GenericParameterAttributes.None :
                        getParameterVariance(i, target, ref genericParamsCache);

                    bool success = t.ContainsGenericParameters ?
                        TryInferGenericParameterBounds(s, t, parameterVariance) :
                        parameterVariance switch {
                            GenericParameterAttributes.Covariant => t.IsAssignableFrom(s),
                            GenericParameterAttributes.Contravariant => s.IsAssignableFrom(t),
                            _ => s == t
                        };

                    if (!success)
                        return false;
                }

                return true;

                static GenericParameterAttributes getParameterVariance(int index, Type target, ref Type[]? genericParams) {
                    genericParams ??= target.GetGenericTypeDefinition().GetGenericArguments();
                    return genericParams[index].GenericParameterAttributes & GenericParameterAttributes.VarianceMask;
                }
            }

            private bool HandleArrayTypes(Type source, Type target, GenericParameterAttributes variance, out bool result) {
                if (source.IsArray || target.IsArray) {
                    if (variance == GenericParameterAttributes.None) {
                        result = source.IsArray && target.IsArray
                            && source.GetArrayRank() == target.GetArrayRank()
                            && TryInferGenericParameterBounds(source.GetElementType(), target.GetElementType(), GenericParameterAttributes.None);
                    } else {
                        ref Type s = ref source;
                        ref Type t = ref target;

                        if (variance == GenericParameterAttributes.Contravariant) {
                            s = ref target;
                            t = ref source;
                        }

                        bool compatibleArrayTypes = s.IsArray && (
                            (t.IsArray && t.GetArrayRank() == s.GetArrayRank()) ||
                            (s.GetArrayRank() == 1 && IsArrayInterface(t)));

                        if (compatibleArrayTypes) {
                            s = s.GetElementType();
                            t = t.IsArray ? t.GetElementType() : t.GenericTypeArguments[0];

                            if (source.IsValueType)
                                variance = GenericParameterAttributes.None;

                            result = TryInferGenericParameterBounds(source, target, variance);
                        } else {
                            result = false;
                        }
                    }

                    return true;
                } else {
                    result = true;
                    return false;
                }
            }

            private static bool IsArrayInterface(Type t) =>
                t.Namespace == "System.Collections.Generic" &&
                Array.IndexOf(ArrayInterfaceNames, t.Name) != -1;


            private void Add(Type genericParameter, Type bound, GenericParameterAttributes variance) {
                int i = Array.IndexOf(_genericParameters, genericParameter);

                switch (variance) {
                    case GenericParameterAttributes.Contravariant:
                        _bounds[i].Upper.Add(bound);
                        break;
                    case GenericParameterAttributes.Covariant:
                        _bounds[i].Lower.Add(bound);
                        break;
                    default:
                        _bounds[i].Exact.Add(bound);
                        break;
                }
            }
        }

#if INSPIRING_REFLECTION
        internal
#endif
        struct ParameterBounds {
            public TypeCollection Upper;

            public TypeCollection Exact;

            public TypeCollection Lower;

            public Type? InferredType;

            public bool TryFixType() {
                InferredType = Exact.Count > 0 ?
                    FixToExactBounds() :
                    (FixToUpperBound() ?? FixToLowerBound());

                return InferredType != null;
            }

            private Type? FixToExactBounds() {
                bool singleCompatibleExactBound =
                    Exact.Count == 1 &&
                    Upper.AllAssignableFrom(Exact[0]) &&
                    Lower.AllAssignableTo(Exact[0]);

                return singleCompatibleExactBound ? Exact[0] : null;
            }

            private Type? FixToUpperBound() {
                for (int i = 0; i < Upper.Count; i++) {
                    if (Upper.AllAssignableFrom(Upper[i])) {
                        return Lower.AllAssignableTo(Upper[i]) ?
                            Upper[i] :
                            null;
                    }
                }

                return null;
            }

            private Type? FixToLowerBound() {
                for (int i = 0; i < Lower.Count; i++) {
                    if (Lower.AllAssignableTo(Lower[i])) {
                        return Upper.AllAssignableFrom(Lower[i]) ?
                            Lower[i] :
                            null;
                    }
                }

                return null;
            }
        }

#if INSPIRING_REFLECTION
        internal
#endif
        struct TypeCollection {
            private object? _items;

            public int Count {
                get => _items switch {
                    null => 0,
                    Type => 1,
                    List<Type> li => li.Count,
                    _ => 0
                };
            }

            public Type this[int index] {
                get => _items switch {
                    Type t when index == 0 => t,
                    List<Type> li => li[index],
                    _ => null!
                };
            }

            public void Add(Type type) {
                switch (_items) {
                    case null:
                        _items = type;
                        break;
                    case Type t when type == t:
                        break;
                    default:
                        AsList().Add(type);
                        break;
                }
            }

            public List<Type> AsList() {
                if (_items is List<Type> list)
                    return list;

                _items = list = _items is Type t ?
                    new List<Type> { t } :
                    new List<Type>();

                return list;
            }

            public bool AllAssignableFrom(Type type) {
                for (int i = 0; i < Count; i++) {
                    if (!this[i].IsAssignableFrom(type)) return false;
                }
                return true;
            }

            public bool AllAssignableTo(Type type) {
                for (int i = 0; i < Count; i++) {
                    if (!type.IsAssignableFrom(this[i])) return false;
                }
                return true;
            }
        }

        private static Func<Type, Type, bool> CreateCanValueSpecialCastDelegate() {
            Type runtimeType = typeof(TypeExtensions).GetType();

            MethodInfo clrMethod = runtimeType
                .GetMethod("CanValueSpecialCast", BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException("CLR method not found. Please inform the author of this library by submitting a Github issue.");

            ParameterExpression from = Expression.Parameter(typeof(Type), "from");
            ParameterExpression to = Expression.Parameter(typeof(Type), "to");

            return Expression
                .Lambda<Func<Type, Type, bool>>(
                    body: Expression.Call(
                        clrMethod,
                        Expression.Convert(from, runtimeType),
                        Expression.Convert(to, runtimeType)),
                    parameters: new[] { from, to })
                .Compile();
        }
    }
}
