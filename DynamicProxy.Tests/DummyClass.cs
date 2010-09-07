using System;
using System.Collections.Generic;
using System.Reflection;

namespace DynamicProxy.Tests
{
    public interface IDummyProperties
    {
        string WritableProperty { get; set; }

        string ReadOnlyProperty { get; }

        int ValueProperty { get; set; }
    }

    public interface IDummyMethods
    {
        string GetStringValue(object input, int anotherInput);

        int GetInt(int input);

        void Statement();
    }

    public interface IDummyEvents
    {
        event SomeDelegate SomeEvent;
        void FireSomeEvent();
    }

    public interface IDummyIndex
    {
        string this[Int32 index] { get; set; }
        string this[string index] { get; set; }
        int this[int index, int anotherIndex] { get; set; }
    }

    public delegate void SomeDelegate(string s);

    class DummyClass
    {
        public static int StaticProperty { get; set; }
        public static int StaticMethod(int input)
        {
            return input;
        }

        private readonly string _readOnlyProperty;
        public DummyClass(string readOnlyPropertyValue)
        {
            _readOnlyProperty = readOnlyPropertyValue;
        }

        public string WritableProperty { get; set; }

        public string ReadOnlyProperty
        {
            get { return _readOnlyProperty; }
        }

        public int ValueProperty { get; set; }

        public int WritableValueTypeProperty { get; set; }

        public Dictionary<int, string> IndexableProperty { get; set; }

        private string GetStringValue(object input, int anotherInput)
        {
            return input + " - " + anotherInput;
        }

        public void Statement()
        {
            return;
        }

        public int GetInt(int input)
        {
            return input;
        }

        public int FunctionWithOutParameter(int value, out string output)
        {
            output = "Output";
            return value;
        }

        public int FunctionWithOutParameter(string value, out string output)
        {
            output = value;
            return 1;
        }

        public event SomeDelegate SomeEvent;

        public void FireSomeEvent()
        {
            SomeEvent += SomeAction;
            SomeEvent("");
        }

        private void SomeAction(string s)
        {

        }

        private string _indexValue = "IndexValue";
        private int _anotherIndexValue;

        public string this[int index]
        {
            get { return _indexValue; }
            set { _indexValue = index + " - " + value; }
        }

        public string this[string index]
        {
            get { return _indexValue; }
            set { _indexValue = index + " - " + value; }
        }

        public int this[int index, int anotherIndex]
        {
            get
            {
                return index + anotherIndex + _anotherIndexValue;
            }
            set
            {
                _anotherIndexValue = index + anotherIndex + value;
            }
        }
    }

    class DummyClass2
    {
        public void FireSomeEvent() { }

    }
}