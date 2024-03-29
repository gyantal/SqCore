﻿using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using SqCommon;

namespace QuantConnect.Exceptions
{
    /// <summary>
    /// Interprets exceptions using the configured interpretations
    /// </summary>
    public class StackExceptionInterpreter : IExceptionInterpreter
    {
        private readonly List<IExceptionInterpreter> _interpreters;

        /// <summary>
        /// Stack interpreter instance
        /// </summary>
        public static readonly Lazy<StackExceptionInterpreter> Instance = new Lazy<StackExceptionInterpreter>(
            () => StackExceptionInterpreter.CreateFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));

        /// <summary>
        /// Determines the order that an instance of this class should be called
        /// </summary>
        public int Order => 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="StackExceptionInterpreter"/> class
        /// </summary>
        /// <param name="interpreters">The interpreters to use</param>
        public StackExceptionInterpreter(IEnumerable<IExceptionInterpreter> interpreters)
        {
            _interpreters = interpreters.OrderBy(x => x.Order).ToList();
        }

        /// <summary>
        /// Gets the interpreters loaded into this instance
        /// </summary>
        public IEnumerable<IExceptionInterpreter> Interpreters => _interpreters;

        /// <summary>
        /// Determines if this interpreter should be applied to the specified exception.
        /// </summary>
        /// <param name="exception">The exception to check</param>
        /// <returns>True if the exception can be interpreted, false otherwise</returns>
        public bool CanInterpret(Exception exception)
        {
            return _interpreters.Any(interpreter => interpreter.CanInterpret(exception));
        }

        /// <summary>
        /// Interprets the specified exception into a new exception
        /// </summary>
        /// <param name="exception">The exception to be interpreted</param>
        /// <param name="innerInterpreter">An interpreter that should be applied to the inner exception.
        /// This provides a link back allowing the inner exceptions to be interpreted using the intepretators
        /// configured in the <see cref="StackExceptionInterpreter"/>. Individual implementations *may* ignore
        /// this value if required.</param>
        /// <returns>The interpreted exception</returns>
        public Exception Interpret(Exception exception, IExceptionInterpreter innerInterpreter = null)
        {
            if (exception == null)
            {
                return null;
            }

            foreach (var interpreter in _interpreters)
            {
                try
                {
                    if (interpreter.CanInterpret(exception))
                    {
                        // use this instance to interpret inner exceptions as well, unless one was specified
                        return interpreter.Interpret(exception, innerInterpreter ?? this);
                    }
                }
                catch (Exception err)
                {
                    Utils.Logger.Error(err);
                }
            }

            return exception;
        }

        /// <summary>
        /// Combines the exception messages from this exception and all inner exceptions.
        /// </summary>
        /// <param name="exception">The exception to create a collated message from</param>
        /// <returns>The collate exception message</returns>
        public string GetExceptionMessageHeader(Exception exception)
        {
            return string.Join(" ", InnersAndSelf(exception).Select(e => e.Message));
        }

        /// <summary>
        /// Creates a new <see cref="StackExceptionInterpreter"/> by loading implementations with default constructors from the specified assemblies
        /// </summary>
        /// <param name="assemblies">The assemblies to scan</param>
        /// <returns>A new <see cref="StackExceptionInterpreter"/> containing interpreters from the specified assemblies</returns>
        public static StackExceptionInterpreter CreateFromAssemblies(IEnumerable<Assembly> assemblies)
        {
            var interpreters =
                from assembly in assemblies
                from type in assembly.GetTypes()
                // ignore non-public and non-instantiable abstract types
                where type.IsPublic && !type.IsAbstract
                // type implements IExceptionInterpreter
                where typeof(IExceptionInterpreter).IsAssignableFrom(type)
                // type is not mocked with MOQ library
                where type.FullName != null && !type.FullName.StartsWith("Castle.Proxies.ObjectProxy")
                // type has default parameterless ctor
                where type.GetConstructor(new Type[0]) != null
                // provide guarantee of deterministic ordering
                orderby type.FullName
                select (IExceptionInterpreter) Activator.CreateInstance(type);

            var stackExceptionInterpreter = new StackExceptionInterpreter(interpreters);

            foreach (var interpreter in stackExceptionInterpreter.Interpreters)
            {
                Utils.Logger.Debug($"Loaded ExceptionInterpreter: {interpreter.GetType().Name}");
            }

            return stackExceptionInterpreter;
        }

        private IEnumerable<Exception> InnersAndSelf(Exception exception)
        {
            yield return exception;
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
                yield return exception;
            }
        }
    }
}
