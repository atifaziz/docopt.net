#nullable enable

namespace DocoptNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    interface IApplicationResultAccumulator<T>
    {
        T New();
        T Command(T state, string name, bool value);
        T Command(T state, string name, int value);
        T Argument(T state, string name);
        T Argument(T state, string name, string value);
        T Argument(T state, string name, StringList value);
        T Option(T state, string name);
        T Option(T state, string name, bool value);
        T Option(T state, string name, string value);
        T Option(T state, string name, int value);
        T Option(T state, string name, StringList value);
        T Error(DocoptBaseException exception);
    }

    static class ApplicationResultAccumulators
    {
        public static readonly IApplicationResultAccumulator<IDictionary<string, Value>> ValueDictionary = new ValueDictionaryAccumulator();
        public static readonly IApplicationResultAccumulator<IDictionary<string, ValueObject>> ValueObjectDictionary = new ValueObjectDictionaryAccumulator();

        sealed class ValueDictionaryAccumulator : IApplicationResultAccumulator<IDictionary<string, Value>>
        {
            public IDictionary<string, Value> New() => new Dictionary<string, Value>();
            public IDictionary<string, Value> Command(IDictionary<string, Value> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, Value> Command(IDictionary<string, Value> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name) => Adding(state, name, Value.None);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, Value> Argument(IDictionary<string, Value> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name) => Adding(state, name, Value.None);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, Value> Option(IDictionary<string, Value> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, Value> Error(DocoptBaseException exception) => null!;

            static IDictionary<string, Value> Adding(IDictionary<string, Value> dict, string name, Value value)
            {
                dict[name] = value;
                return dict;
            }
        }

        sealed class ValueObjectDictionaryAccumulator : IApplicationResultAccumulator<IDictionary<string, ValueObject>>
        {
            public IDictionary<string, ValueObject> New() => new Dictionary<string, ValueObject>();
            public IDictionary<string, ValueObject> Command(IDictionary<string, ValueObject> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Command(IDictionary<string, ValueObject> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name) => Adding(state, name, null);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Argument(IDictionary<string, ValueObject> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name) => Adding(state, name, null);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, bool value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, string value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, int value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Option(IDictionary<string, ValueObject> state, string name, StringList value) => Adding(state, name, value);
            public IDictionary<string, ValueObject> Error(DocoptBaseException exception) => null!;

            static IDictionary<string, ValueObject> Adding(IDictionary<string, ValueObject> dict, string name, object? value)
            {
                dict[name] = new ValueObject(value);
                return dict;
            }
        }

        public static IApplicationResultAccumulator<T>
            Create<T>(params (string Name, Expression<Func<T, object?>> Expression)[] bindings)
            where T : new()
        {
            return Create(_ => default!, (b, _) => b.Name, b => b.Expression, bindings);
        }

        public static IApplicationResultAccumulator<T>
            Create<T>(params Expression<Func<T, object?>>[] bindings)
            where T : new()
        {
            return Create(_ => default!, bindings);
        }

        public static IApplicationResultAccumulator<T>
            Create<T>(Func<DocoptBaseException, T> error,
                      params Expression<Func<T, object?>>[] bindings)
            where T : new()
        {
            return Create(error, p => InferName(p.Name), b => b, bindings);

            static string InferName(string name)
            {
                const string argPrefix = "Arg";
                const string flagPrefix = "Flag";

                return name.StartsWith(argPrefix, StringComparison.Ordinal) ? $"<{Kebabise(name.Substring(3))}>"
                     : name.StartsWith(flagPrefix, StringComparison.Ordinal) ? $"--{Kebabise(name.Substring(4))}"
                     : Kebabise(name);
            }

            static string Kebabise(string pascalString)
            {
                var kebabBuilder = new StringBuilder();
                foreach (var ch in pascalString)
                {
                    if (char.IsUpper(ch))
                    {
                        if (kebabBuilder.Length > 0)
                            kebabBuilder.Append('-');
                        kebabBuilder.Append(char.ToLowerInvariant(ch));
                    }
                    else
                    {
                        kebabBuilder.Append(ch);
                    }
                }
                return kebabBuilder.ToString();
            }
        }

        public static IApplicationResultAccumulator<T>
            Create<T, TBinding>(Func<DocoptBaseException, T> error,
                                Func<PropertyInfo, string> nameSelector,
                                Func<TBinding, Expression<Func<T, object?>>> expressionSelector,
                                params TBinding[] bindings)
            where T : new()
        {
            return Create(error, (_, p) => nameSelector(p), expressionSelector, bindings);
        }

        public static IApplicationResultAccumulator<T> Create<T, TBinding>(
            Func<DocoptBaseException, T> error,
            Func<TBinding, PropertyInfo, string> nameSelector,
            Func<TBinding, Expression<Func<T, object?>>> expressionSelector,
            params TBinding[] bindings)
            where T : new()
        {
            if (nameSelector is null) throw new ArgumentNullException(nameof(nameSelector));
            if (expressionSelector is null) throw new ArgumentNullException(nameof(expressionSelector));
            if (bindings is null) throw new ArgumentNullException(nameof(bindings));

            var map = new Dictionary<string, Action<T, object?>>();

            var n = 1;
            foreach (var binding in bindings)
            {
                var expression = expressionSelector(binding);
                if (CreateAssignment(expression) is ({ } property, { } assignment))
                    map.Add(nameSelector(binding, property), assignment.Compile());
                else
                    throw new ArgumentException($"Expression type for binding #{n} is unsupported.", nameof(bindings));
                n++;
            }

            return new ObjectAccumulator<T>(error, map);

            static (PropertyInfo, Expression<Action<T, object?>>)?
                CreateAssignment<TValue>(Expression<Func<T, TValue>> expression)
            {
                var property = expression.Body switch
                {
                    MemberExpression { Member: PropertyInfo pi } => pi,
                    UnaryExpression { NodeType: ExpressionType.Convert, Operand: MemberExpression { Member: PropertyInfo pi } } => pi,
                    _ => null,
                };

                if (property is null)
                    return null;

                var target = Expression.Parameter(typeof(T));
                var value = Expression.Parameter(typeof(object));
                var body = Expression.Assign(Expression.MakeMemberAccess(target, property),
                                             Expression.Convert(value, property.PropertyType));
                return (property, Expression.Lambda<Action<T, object?>>(body, target, value));
            }
        }

        sealed class ObjectAccumulator<T> : IApplicationResultAccumulator<T>
            where T : new()
        {
            readonly Func<DocoptBaseException, T> _error;
            readonly IDictionary<string, Action<T, object?>> _bindings;

            public ObjectAccumulator(Func<DocoptBaseException, T> error,
                                    IDictionary<string, Action<T, object?>> bindings) =>
                (_error, _bindings) = (error, bindings);

            public T New() => new();
            public T Command(T state, string name, bool value) => Adding(state, name, value);
            public T Command(T state, string name, int value) => Adding(state, name, value);
            public T Argument(T state, string name) => Adding(state, name, null);
            public T Argument(T state, string name, string value) => Adding(state, name, value);
            public T Argument(T state, string name, StringList value) => Adding(state, name, value);
            public T Option(T state, string name) => Adding(state, name, null);
            public T Option(T state, string name, bool value) => Adding(state, name, value);
            public T Option(T state, string name, string value) => Adding(state, name, value);
            public T Option(T state, string name, int value) => Adding(state, name, value);
            public T Option(T state, string name, StringList value) => Adding(state, name, value);
            public T Error(DocoptBaseException exception) => _error(exception);

            T Adding(T args, string name, object? value)
            {
                _bindings[name](args, value);
                return args;
            }
        }
    }
}
