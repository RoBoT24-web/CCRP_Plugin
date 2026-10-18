using Newtonsoft.Json;
using System.IO;

namespace CCRPPlugin.Storage
{
    public class DataStorage<T> where T : class
    {
        public string DataPath { get; private set; }
        private readonly Formatting formatting;

        public DataStorage(string dir, string fileName, Formatting formatting = Formatting.Indented)
        {
            DataPath = Path.Combine(dir, fileName);
            this.formatting = formatting;
        }

        public void Save(T obj)
        {
            string objData = JsonConvert.SerializeObject(obj, formatting);
            string temporaryPath = DataPath + ".tmp";

            using (StreamWriter stream = new StreamWriter(temporaryPath, false))
            {
                stream.Write(objData);
            }

            if (File.Exists(DataPath))
            {
                File.Replace(temporaryPath, DataPath, null);
            }
            else
            {
                File.Move(temporaryPath, DataPath);
            }
        }

        public T Read()
        {
            if (File.Exists(DataPath))
            {
                string dataText;
                using (StreamReader stream = File.OpenText(DataPath))
                {
                    dataText = stream.ReadToEnd();
                }
                return JsonConvert.DeserializeObject<T>(dataText);
            }
            else
            {
                return null;
            }
        }
    }
}
