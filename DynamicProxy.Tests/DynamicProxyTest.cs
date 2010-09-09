using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DynamicProxy.Tests
{
    [TestClass]
    public class DynamicProxyTest
    {
        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Redirect_Property_Gets_To_The_Given_Instance()
        {
            dynamic proxy = new DynamicProxy(new DummyClass("TestString"));
            Assert.AreEqual("TestString", proxy.ReadOnlyProperty);
            Assert.AreEqual(0, proxy.WritableValueTypeProperty);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Redirect_Property_Sets_To_The_Given_Instance()
        {
            dynamic proxy = new DynamicProxy(new DummyClass(""));

            Assert.IsNull(proxy.WritableProperty);
            proxy.WritableProperty = "TestString";
            Assert.AreEqual("TestString", proxy.WritableProperty);

            proxy.WritableValueTypeProperty = 1;
            Assert.AreEqual(1, proxy.WritableValueTypeProperty);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Map_Static_Properties_To_The_Given_Instance()
        {
            dynamic proxy = new DynamicProxy(new DummyClass(""));

            proxy.StaticProperty = 11;
            Assert.AreEqual(11, proxy.StaticProperty);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        [ExpectedException(typeof(RuntimeBinderException), AllowDerivedTypes = false)]
        public void DynamicProxy_Should_Throw_RuntimeBinderException_When_Property_Is_Not_Writable()
        {
            dynamic proxy = new DynamicProxy(new DummyClass(""));

            proxy.ReadOnlyProperty = "";
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Map_Instance_Method_Calls_To_The_Given_Instance()
        {
            const string instance = "TestString";
            dynamic proxy = new DynamicProxy(instance);

            char[] toCharArrayResult = proxy.ToCharArray();
            char[] toCharArrayIndexResult = proxy.ToCharArray(0, 4);

            var copyResult = new char[9];
            proxy.CopyTo(0, copyResult, 0, 9);

            Assert.AreEqual(instance, new string(toCharArrayResult));
            Assert.AreEqual(new string(instance.ToCharArray(0, 4)), new string(toCharArrayIndexResult));
            Assert.AreEqual(instance.Substring(0, 9), new string(copyResult));
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Map_Static_Method_Calls_To_The_Given_Instance()
        {
            dynamic proxy = new DynamicProxy(new DummyClass(""));

            Assert.AreEqual(12, proxy.StaticMethod(12));
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Get_Indexed_Elements_For_Array_Types()
        {
            dynamic proxy = new DynamicProxy(new[] { "One", "Two" });

            Assert.AreEqual("One", proxy[0]);
            Assert.AreEqual("Two", proxy[1]);

            var multiArray = new string[2, 2];
            multiArray[0, 0] = "Three";
            multiArray[0, 1] = "Four";
            multiArray[1, 0] = "Five";
            proxy = new DynamicProxy(multiArray);

            Assert.AreEqual("Three", proxy[0, 0]);
            Assert.AreEqual("Four", proxy[0, 1]);
            Assert.AreEqual("Five", proxy[1, 0]);
            Assert.IsNull(proxy[1, 1]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Get_Indexed_Elements_For_IDictionary_Types()
        {
            var dictionary = new Dictionary<string, int>(1)
                                 {
                                     {"One", 1}
                                 };
            dynamic proxy = new DynamicProxy(dictionary);
            Assert.AreEqual(1, proxy["One"]);

            var hash = new Hashtable
                           {
                               {"Two", 2}
                           };
            proxy = new DynamicProxy(hash);
            Assert.AreEqual(2, proxy["Two"]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Set_Indexed_Elements_For_Array_Types()
        {
            dynamic proxy = new DynamicProxy(new[] { "One", "Two" });

            Assert.AreEqual("One", proxy[0]);
            proxy[0] = "Three";
            Assert.AreEqual("Three", proxy[0]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Set_Indexed_Elements_For_IDictionary_Types()
        {
            var dictionary = new Dictionary<string, int>(1)
                                 {
                                     {"One", 1}
                                 };
            dynamic proxy = new DynamicProxy(dictionary);

            Assert.AreEqual(1, proxy["One"]);
            proxy["One"] = 2;
            Assert.AreEqual(2, proxy["One"]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Return_An_Instance_Of_The_Proxied_Object_For_Casts_If_The_Conversion_Is_Possible()
        {
            dynamic proxy = new DynamicProxy(new[] { 1, 2 });

            var results = new List<int>();

            IEnumerable integers = proxy;
            foreach (int integer in integers)
            {
                results.Add(integer);
            }

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(1, results[0]);
            Assert.AreEqual(2, results[1]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Map_Method_Calls_With_Out_Parameters_To_Given_Instance()
        {
            dynamic proxy = new DynamicProxy(new DummyClass(""));

            string output;

            Assert.AreEqual(1, proxy.FunctionWithOutParameter(1, out output));
            Assert.AreEqual("Output", output);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Return_An_Object_Implementing_The_Requested_Interfaces_Properties()
        {
            dynamic proxy = new DynamicProxy(new DummyClass("NewString"));
            IDummyProperties iDummy = proxy;

            const string written = "Written";

            Assert.AreEqual("NewString", iDummy.ReadOnlyProperty);

            iDummy.WritableProperty = written;
            Assert.AreEqual(written, iDummy.WritableProperty);

            iDummy.ValueProperty = 3;
            Assert.AreEqual(3, iDummy.ValueProperty);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Return_An_Object_Implementing_The_Requested_Interfaces_Methods()
        {
            dynamic proxy = new DynamicProxy(new DummyClass("NewString"));
            IDummyMethods iDummy = proxy;

            Assert.AreEqual("1 - 2", iDummy.GetStringValue(1, 2));
            Assert.AreEqual(12, iDummy.GetInt(12));

            iDummy.Statement();
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Return_An_Object_Implementing_The_Requested_Interfaces_Indexes()
        {
            dynamic proxy = new DynamicProxy(new DummyClass("NewString"));
            IDummyIndex iDummy = proxy;

            iDummy[123456] = "SomeText";
            Assert.AreEqual("123456 - SomeText", iDummy[123456]);

            iDummy["index"] = "SomeNewText";
            Assert.AreEqual("index - SomeNewText", iDummy["index"]);

            iDummy[1, 2] = 3;
            Assert.AreEqual(9, iDummy[1, 2]);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Return_An_Object_Implementing_The_Requested_Interfaces_Events()
        {
            dynamic proxy = new DynamicProxy(new DummyClass("NewString"));
            IDummyEvents iDummy = proxy;
            string result = null;

            SomeDelegate someEvent = (a => result = "NewValue!");
            iDummy.SomeEvent += someEvent;
            iDummy.FireSomeEvent();
            Assert.AreEqual("NewValue!", result);

            result = null;
            iDummy.SomeEvent -= someEvent;
            iDummy.FireSomeEvent();
            Assert.IsNull(result);
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        [ExpectedException(typeof(RuntimeBinderException), AllowDerivedTypes = false)]
        public void DynamicProxy_Should_Throw_An_Exception_For_Casts_If_The_Conversion_Is_Not_Possible_Because_Methods_Do_Not_Match()
        {
            dynamic proxy = new DynamicProxy("");
            IDisposable iDisposable = proxy;

            iDisposable.Dispose();
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        [ExpectedException(typeof(RuntimeBinderException), AllowDerivedTypes = false)]
        public void DynamicProxy_Should_Throw_An_Exception_For_Casts_If_The_Conversion_Is_Not_Possible_Because_Properties_Do_Not_Match()
        {
            dynamic proxy = new DynamicProxy("");
            IDummyProperties iDummy = proxy;
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        [ExpectedException(typeof(RuntimeBinderException), AllowDerivedTypes = false)]
        public void DynamicProxy_Should_Throw_An_Exception_For_Casts_If_The_Conversion_Is_Not_Possible_Because_Indexers_Do_Not_Match()
        {
            dynamic proxy = new DynamicProxy("");
            IDummyIndex iDummy = proxy;
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        [ExpectedException(typeof(RuntimeBinderException), AllowDerivedTypes = false)]
        public void DynamicProxy_Should_Throw_An_Exception_For_Casts_If_The_Conversion_Is_Not_Possible_Because_Events_Do_Not_Match()
        {
            dynamic proxy = new DynamicProxy(new DummyClass2());
            IDummyEvents iDummy = proxy;
        }

        [TestMethod]
        [DeploymentItem("DynamicProxy.dll")]
        public void DynamicProxy_Should_Map_Members_To_Given_Instance_When_Instance_Is_A_Dynamic_Type()
        {
            var dummy = new DummyClass("String Value");
            dynamic proxy = new DynamicProxy(new DynamicProxy(new DynamicProxy(new DynamicProxy(dummy))));

            Assert.AreEqual(dummy.ReadOnlyProperty, proxy.ReadOnlyProperty);
            Assert.AreEqual(dummy.ReadOnlyProperty.Length, proxy.ReadOnlyProperty.Length);
            Assert.IsTrue(proxy.Equals(dummy));

            Assert.AreEqual(dummy.GetInt(12), proxy.GetInt(12));

            Assert.AreEqual(dummy[1, 2], proxy[1, 2]);
        }

        [ClassCleanup]
        public static void TearDown()
        {
            DynamicInterface_Accessor.SaveAssembly();
        }
    }
}