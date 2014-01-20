using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.ComponentModel;

namespace TransProp.Core
{

    public sealed class PropsDocument
    {
        private List<PropsElement> elements;
        public IEnumerable<PropsElement> Elements { get { return elements; } }


        public bool IsReadOnly { get; private set; }
        public PropsDocument(bool isReadOnly = false)
        {
            IsReadOnly = isReadOnly;
        }

        private class PropsReaderObserver : IObserver<PropsElement>
        {
            public bool HasErrors { get; private set; }
            public Exception Exception { get; private set; }
            public bool Completed { get; private set; }


            public List<PropsElement> Elements { get; private set; }

            internal PropsReaderObserver()
            {
                Elements = new List<PropsElement>();
            }

            public void OnCompleted()
            {
                Completed = true;
            }

            public void OnError(Exception error)
            {
                HasErrors = true;
                Exception = error;
            }

            public void OnNext(PropsElement value)
            {
                if (Completed)
                {
                    throw new InvalidOperationException("Observer completed");
                }
                Elements.Add(value);
            }
        }

        public void Load(string fileName)
        {
            using (FileStream stream = File.OpenRead(fileName))
            {
                Load(stream);
            }
        }

        public PropsDocument()
        {
            elements = new List<PropsElement>();
        }

        public void Load(Stream stream)
        {
            ThrowIfNull(stream, "stream");
            PropsReader reader = new PropsReaderFileReader();
            PropsReaderObserver observer = new PropsReaderObserver();
            reader.Subscribe(observer);
            reader.Parse(stream);

            elements = observer.Elements;
        }

        public void Save(string fileName)
        {
            CheckIsReadOnly();
            using (FileStream stream = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                PropsWriter writer = new PropsWriter();
                writer.Write(stream, Elements);
            }
        }

        public void Remove(string key)
        {
            CheckIsReadOnly();
            elements = elements.Where(e => e.Key != key).ToList();
        }

        public void Add(PropsElement element)
        {
            ThrowIfNull(element, "element");
            elements.Add(element);
        }

        public IEnumerable<string> Get(string key)
        {
            return Elements.Where(e => e.Key == key).Select(e => e.Value);
        }

        private void CheckIsReadOnly()
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException("Document is read only");
            }
        }

        private void ThrowIfNull(object obj, string name)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        public PropsDocument Copy(bool isReadOnly = false, bool onlyKeys = false)
        {
            PropsDocument newDoc = new PropsDocument(isReadOnly);
            foreach (PropsElement element in elements)
            {
                string value = onlyKeys ? string.Empty : element.Value;
                newDoc.elements.Add(new PropsElement(element.Key, value));
            }
            return newDoc;
        }


        public void RemoveComments()
        {
            var comments = elements.Where(e => e.IsComment).ToList();
            foreach (PropsElement el in comments)
            {
                elements.Remove(el);
            }
        }
    }
}
