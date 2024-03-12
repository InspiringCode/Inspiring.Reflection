using System;
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
        private static readonly Func<Type, Type[], Type[]?, Type, bool> SatisfiesConstraints = CreateSatisfiesConstraintsDelegate();

        public static bool SatisfiesGenericConstraints(this MemberInfo member, params Type[] genericArguments) {
            Type[] typeContext;
            Type[] genericParameters;
            Type[]? methodContext = null;

            switch (member) {
                case Type t:
                    genericParameters = t.GetGenericArguments();
                    typeContext = genericArguments;
                    break;
                case MethodInfo m:
                    genericParameters = m.GetGenericArguments();
                    methodContext = genericArguments;
                    typeContext = m.DeclaringType.GetGenericArguments();
                    break;
                default:
                    throw new ArgumentException(nameof(member));
            }

            for (int i = 0; i < genericArguments.Length; i++) {
                Type p = genericParameters[i];
                Type a = genericArguments[i];

                if (!satisfiesMissingParameterAttributeChecks(p, a) || !SatisfiesConstraints(p, typeContext, methodContext, a))
                    return false;
            }

            return true;

            static bool satisfiesMissingParameterAttributeChecks(Type param, Type arg) {
                if ((param.GenericParameterAttributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0)
                    return !arg.IsAbstract;

                return true;
            }
        }

        private static Func<Type, Type[], Type[]?, Type, bool> CreateSatisfiesConstraintsDelegate() {
            Type runtimeType = typeof(TypeExtensions).GetType();

            MethodInfo clrMethod = typeof(RuntimeTypeHandle)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "SatisfiesConstraints" && m.GetParameters().Length == 4)
                .Single();

            MethodInfo convertToRuntimeTypeArray = typeof(TypeExtensions)
                .GetMethod(nameof(ConvertArrayType), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(runtimeType);

            ParameterExpression genericParameter = Expression.Parameter(typeof(Type), "genericParameter");
            ParameterExpression typeContext = Expression.Parameter(typeof(Type[]), "typeContext");
            ParameterExpression methodContext = Expression.Parameter(typeof(Type[]), "methodContext");
            ParameterExpression genericArgument = Expression.Parameter(typeof(Type), "genericArgument");

            return Expression
                .Lambda<Func<Type, Type[], Type[]?, Type, bool>>(
                    body: Expression.Call(
                        clrMethod,
                        Expression.Convert(genericParameter, runtimeType),
                        Expression.Call(convertToRuntimeTypeArray, typeContext),
                        Expression.Call(convertToRuntimeTypeArray, methodContext),
                        Expression.Convert(genericArgument, runtimeType)),
                    parameters: new[] { genericParameter, typeContext, methodContext, genericArgument })
                .Compile();
        }

        private static T[]? ConvertArrayType<T>(Array source) {
            if (source == null)
                return null;

            T[] converted = new T[source.Length];
            source.CopyTo(converted, 0);
            return converted;
        }
    }
}
