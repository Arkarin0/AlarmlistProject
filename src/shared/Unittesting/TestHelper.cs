using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WMSDetector.Tests
{
    public partial class TestHelper
    {
        //public static string GetParameternameOfFunction(System.Reflection.MethodInfo method, int index)
        //{
        //    string value = string.Empty;

        //    if (method == null) return value;

        //    var parameters = method.GetParameters();
        //    if (parameters.Length == 0) return value;

        //    value = parameters.ElementAtOrDefault(index)?.Name;
        //    return value;
        //}
        //public static string GetParameternameOfFunction(System.Reflection.ConstructorInfo constructor, int index)
        //{
        //    string value = string.Empty;

        //    if (constructor == null) return value;

        //    var parameters = constructor.GetParameters();
        //    if (parameters.Length == 0) return value;

        //    value = parameters.ElementAtOrDefault(index)?.Name;
        //    return value;
        //}

        /// <summary>
        /// Gets the methode information.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression">The expression.</param>
        /// <returns>System.Reflection.MethodInfo.</returns>
        /// <remarks>
        /// usage: <c>MethodInfo mi = MethodInfoHelper.GetMethodInfo&lt;Program&gt;(x => x.Test());</c>
        /// </remarks>
        /// <example>
        /// How to use:
        /// <code>
        ///     MethodInfo mi = MethodInfoHelper.GetMethodInfo&lt;Program&gt;(x => x.Test());
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">Expression is not a methode. - expression</exception>
        public static System.Reflection.MethodInfo GetMethodeInfo<T>(Expression<Action<T>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member == null) throw new ArgumentException("Expression is not a methode.", nameof(expression));

            return member.Method;
        }

        public static List<string> GetPropertieNames(IEnumerable<Assert.RaisedEvent<PropertyChangedEventArgs>> list)
        {
            return list.Select(item => item.Arguments.PropertyName).ToList();
        }

     
        public static void ChangePropertyTest<T>(Action action, Func<T> getPropertyValue, T expectedValue)
        {
            action();
            T actual = getPropertyValue();
            Assert.Equal(expectedValue, actual);
        }

        public static T ActionThrows<T>(Action action) where T:Exception
        {
            return Assert.Throws<T>(action);
        }
        public static T ActionThrows<T>(Action action, string expectedMessage) where T : Exception
        {
            var ex = ActionThrows<T>(action);
            Assert.Equal(expectedMessage, ex.Message);
            return ex;
        }

        public static InvalidOperationException ActionThrowsInvalidIOperatrion(Action action)
        {
            return ActionThrows<InvalidOperationException>(action);
        }
        public static InvalidOperationException ActionThrowsInvalidIOperatrion(Action action, string expectedMessage)
        {
            return ActionThrows<InvalidOperationException>(action, expectedMessage);
        }

        public static ArgumentOutOfRangeException ActionThrowsArgumentOutOfRangeException(Action action, string propertyName)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(action);

            Assert.Equal(propertyName, ex.ParamName);

            return ex;
        }
        public static IEnumerable<ArgumentOutOfRangeException> ActionsThrowsArgumentOutOfRangeException(string propertyName, params Action[] actions)
        {
            var unexpectedErrors = new Stack<Tuple<int, string,Exception>>();
            var expectedErrors = new Stack<ArgumentOutOfRangeException>();
            int count = actions.Count();
            
            for (int i = 0; i < count; i++)
            {
                Action action = actions.ElementAt(i);
                try
                {
                    var ex = Assert.Throws<ArgumentOutOfRangeException>(action);
                    expectedErrors.Push(ex);
                    Assert.Equal(propertyName, ex.ParamName);
                }
                catch (Exception ex)
                {
                    unexpectedErrors.Push(new Tuple<int, string, Exception>(i,propertyName,ex));
                }
            }

            if (unexpectedErrors.Count > 0) throw Xunit.Sdk.AllException.ForFailures(count, unexpectedErrors.ToArray());
            
            

            return expectedErrors;
        }
        public static ArgumentNullException ActionThrowsArgumentNullException(Action action, string propertyName)
        {
            var ex= Assert.Throws<ArgumentNullException>(action);

            Assert.Equal(propertyName, ex.ParamName);

            return ex;
        }
        public static ArgumentException ActionThrowsArgumentException(Action action, string propertyName)
        {
            var ex = Assert.Throws<ArgumentException>(action);

            Assert.Equal(propertyName, ex.ParamName);

            return ex;
        }

        private static IEnumerable<Assert.RaisedEvent<PropertyChangedEventArgs>> RaisePropertyChangedEvent(INotifyPropertyChanged obj, Action action)
        {
            var list = new List<Assert.RaisedEvent<PropertyChangedEventArgs>>();

            Action<object, PropertyChangedEventArgs> ev = (object sender, PropertyChangedEventArgs args) =>
            {
                list.Add(new Assert.RaisedEvent<PropertyChangedEventArgs>(sender, args));
            };

            obj.PropertyChanged += ev.Invoke;
            action.Invoke();
            obj.PropertyChanged -= ev.Invoke;

            return list;
        }

        public static Assert.RaisedEvent<PropertyChangedEventArgs> RaisesPropertyChangedEvent(INotifyPropertyChanged obj, Action changevalue, string propertyName)
        {
            var list = RaisePropertyChangedEvent(obj, changevalue);
            var actual = GetPropertieNames(list);
            if (!actual.Contains(propertyName)) throw Xunit.Sdk.PropertyChangedException.ForUnsetProperty(propertyName);
            //Assert.True(actual.Contains(propertyName), $"The PropertyChangedEvent for the Property {propertyName} was not raised.");
            //Assert.Contains(propertyName, actual);

            return list.First(item => item.Arguments.PropertyName == propertyName);
        }

        public static IEnumerable<Assert.RaisedEvent<PropertyChangedEventArgs>> RaisesMultiplePropertyChangedEvents(INotifyPropertyChanged obj, Action changevalue, params string[] propertyNames)
        {
            var list = RaisePropertyChangedEvent(obj, changevalue);
            var actual = GetPropertieNames(list);

            //foreach (var propertyName in propertyNames)
            //{
            //    Assert.True(actual.Contains(propertyName), $"The PropertyChangedEvent for the Property {propertyName} was not raised.");
            //}

            //Assert.All(propertyNames, item => Assert.Contains(item, actual));
            Assert.All(propertyNames, propertyName => { if (!actual.Contains(propertyName)) throw Xunit.Sdk.PropertyChangedException.ForUnsetProperty(propertyName); });

            return list;
        }

        /// <summary>
        /// Verifies that an event with the exact event args is raised.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments to expect.</typeparam>
        /// <param name="attach">Code to attach the evnt handler.</param>
        /// <param name="detach">Code to detach the evnt handler.</param>
        /// <param name="action">A delegaate to the code to be tested</param>
        /// <returns>The event sender and arguments wrapped in an object.</returns>
        /// <exception cref="Xunit.Sdk.RaisesException">Throw when the expected event was not raised.</exception>
        public static Assert.RaisedEvent<EventArgs> RaisesEvent( Action<EventHandler> attach, Action<EventHandler> detach, Action action)
        {
            Assert.RaisedEvent<EventArgs> raisedEvent = null;
            EventHandler ev = (s, e) => 
            {
                raisedEvent = new Assert.RaisedEvent<EventArgs>(s, e);
            };

            attach(ev);
            action();
            detach(ev);

            if (raisedEvent == null) 
                throw Xunit.Sdk.RaisesException.ForNoEvent(typeof(EventArgs));
            
            if(raisedEvent.Arguments!= null && !raisedEvent.Arguments.GetType().Equals(typeof(EventArgs)))
                throw Xunit.Sdk.RaisesException.ForIncorrectType(typeof(EventArgs),raisedEvent.Arguments.GetType());

            return raisedEvent;
        }

        /// <summary>
        /// Verifies that an event with the exact event args is raised.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments to expect.</typeparam>
        /// <param name="attach">Code to attach the evnt handler.</param>
        /// <param name="detach">Code to detach the evnt handler.</param>
        /// <param name="action">A delegaate to the code to be tested</param>
        /// <returns>The event sender and arguments.</returns>
        /// <exception cref="Xunit.Sdk.RaisesException">Throw when the expected event was not raised.</exception>
        public static EventHandler<T> RaisesEvent<T>(Action<EventHandler<T>> attach, Action<EventHandler<T>> detach, Action action)
        {
            bool wasRaised = false;
            object sender = null;
            T args = default;
            EventHandler<T> ev = (s, e) =>
            {
                wasRaised = true;
                sender = s;
                args = e;
            };

            attach(ev);
            action();
            detach(ev);

            if (!wasRaised)
                throw Xunit.Sdk.RaisesException.ForNoEvent(typeof(T));

            if (args != null && !args.GetType().Equals(typeof(T)))
                throw Xunit.Sdk.RaisesException.ForIncorrectType(typeof(T), args.GetType());

            return ev;
        }

        /// <summary>
        /// Verifies that an event with the exact or a derived event args is raised.
        /// </summary>
        /// <typeparam name="T">The type of the event arguments to expect.</typeparam>
        /// <param name="attach">Code to attach the evnt handler.</param>
        /// <param name="detach">Code to detach the evnt handler.</param>
        /// <param name="action">A delegaate to the code to be tested</param>
        /// <returns>The event sender and arguments.</returns>
        /// <exception cref="Xunit.Sdk.RaisesException">Throw when the expected event was not raised.</exception>
        public static (object sender, T arguments) RaisesEventAny<T>(Action<EventHandler<T>> attach, Action<EventHandler<T>> detach, Action action)
        {
            bool wasRaised = false;
            object sender = null;
            T args = default;
            EventHandler<T> ev = (s, e) =>
            {
                wasRaised = true;
                sender = s;
                args = e;
            };

            attach(ev);
            action();
            detach(ev);

            if (!wasRaised)
                throw Xunit.Sdk.RaisesException.ForNoEvent(typeof(T));

            if (args != null && !typeof(T).IsAssignableFrom(args.GetType()))
                throw Xunit.Sdk.RaisesException.ForIncorrectType(typeof(T), args.GetType());

            return (sender,args);
        }

        public static void AssertWasCalled(Action<Action> attach, Action detach, Action action)
        {
            bool wasCalled = false;
            Action test = () => { wasCalled = true; };

            attach(test);
            action();
            detach();

            if (!wasCalled) throw new WasCalledException();
        }

        public static void AssertWasCalled(Func<bool> validate, Action action)
        {

            action();
            bool wasCalled = validate();

            if (!wasCalled) throw new WasCalledException();
        }

        public static Task StartSTATask(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(new object());
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        public static Task StartTask(Action action, System.Threading.ThreadPriority priority= System.Threading.ThreadPriority.Normal)
        {
            return Task.Factory.StartNew(() => 
            {
                try
                {
                    System.Threading.Thread.CurrentThread.Priority = priority;
                    action();
                }
                finally
                {
                    System.Threading.Thread.CurrentThread.Priority =  System.Threading.ThreadPriority.Normal;
                }                
            });
        }

        public static void Sleep(int milis=15)
        {
            System.Threading.Thread.Sleep(milis);
        }

        public static void AssertUI(Action yourTestCode)
        {
            StartSTATask(yourTestCode).Wait();
        }

        /// <summary>
        /// Waits the specified millisecounds.
        /// </summary>
        /// <param name="milis">The milis. default is 1000</param>
        public static void Wait(int milis= 1000)
        {
            System.Threading.Thread.Sleep(milis);
        }

        /// <summary>
        /// Tries the specified action.
        /// </summary>
        /// <typeparam name="T">The exception to catch and ignore.</typeparam>
        /// <param name="action">The action.</param>
        /// <param name="waitMillis">Wait the specified millisecounds before trying the next attempt.</param>
        /// <param name="maxAtempts">The number of failed attempts before an <see cref="Exception"/> is thrown.</param>
        /// <exception cref="Xunit.Sdk.XunitException">Failed {maxAtempts} times because of the {typeof(T)}:\n {exception}</exception>
        public static void Try<T>(Action action, int waitMillis = 500, int maxAtempts=10) where T:Exception
        {
            int atempts = 0;
            bool tryAgain = false;
            Exception exception = null;
            do
            {
                tryAgain = false;

                try
                {
                    action();
                    exception = null;
                }
                catch (T ex)
                {
                    atempts++;
                    exception = ex;
                    tryAgain = true;
                    TestHelper.Sleep(waitMillis);
                }
            } while (tryAgain && atempts < maxAtempts);

            if (exception != null) throw new Xunit.Sdk.XunitException($"Failed {maxAtempts} times because of the {typeof(T)}:\n {exception}");
        }
        /// <summary>
        /// Tries the specified action.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="waitMillis">The wait millis.</param>
        public static void Try(Action action, int waitMillis = 500)
        => Try<Exception>(action, waitMillis);
    }

    /// <summary>
    /// Exception Throw when WasCalled unexpectedly was not called.
    /// Implements the <see cref="Xunit.Sdk.XunitException" />
    /// </summary>
    /// <seealso cref="Xunit.Sdk.XunitException" />
    public class WasCalledException: Xunit.Sdk.XunitException
    {
        public WasCalledException():base("The expected action was not called.")
        { }
    }
}
