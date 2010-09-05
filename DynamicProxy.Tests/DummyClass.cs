using System;
using System.Collections.Generic;

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
        event Action<string> SomeEvent;
    }

    public interface IDummyIndex
    {
        string this[Int32 index] { get; set; }
        string this[string index] { get; set; }
        int this[int index, int anotherIndex] { get; set; }
    }

    internal class DummyClass
    {
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

        public string GetStringValue(object input, int anotherInput)
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

        public event Action<string> SomeEvent;

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
}