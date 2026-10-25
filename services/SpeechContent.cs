using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace MeoPet
{
    internal sealed class SpeechLine
    {
        public string Text { get; set; }
        public string Expression { get; set; }
    }

    internal sealed class SpeechContent
    {
        private readonly IDictionary<string, SpeechLine[]> _speech;
        private readonly IDictionary<string, string> _expressions;
        private readonly Random _random = new Random();

        private SpeechContent(
            IDictionary<string, SpeechLine[]> speech,
            IDictionary<string, string> expressions)
        {
            _speech = speech;
            _expressions = expressions;
        }

        public static SpeechContent Load()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary<string, SpeechLine[]> speech = Deserialize<IDictionary<string, SpeechLine[]>>(
                serializer,
                "MeoPet.data.speech.json");
            IDictionary<string, string> expressions = Deserialize<IDictionary<string, string>>(
                serializer,
                "MeoPet.data.expressions.json");

            ValidateExpressions(expressions);
            ValidateSpeech(speech, expressions);
            return new SpeechContent(speech, expressions);
        }

        public SpeechLine GetRandomLine(string category)
        {
            SpeechLine[] lines;
            if (!_speech.TryGetValue(category, out lines))
            {
                throw new InvalidDataException("找不到對話類別：" + category);
            }

            return lines[_random.Next(lines.Length)];
        }

        public string GetExpression(string name)
        {
            string expression;
            if (!_expressions.TryGetValue(name, out expression))
            {
                throw new InvalidDataException("找不到表情：" + name);
            }

            return expression;
        }

        private static T Deserialize<T>(JavaScriptSerializer serializer, string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidDataException("找不到對話資料檔：" + resourceName);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    T result = serializer.Deserialize<T>(reader.ReadToEnd());
                    if (result == null)
                    {
                        throw new InvalidDataException("對話資料檔內容為空：" + resourceName);
                    }

                    return result;
                }
            }
        }

        private static void ValidateSpeech(
            IDictionary<string, SpeechLine[]> speech,
            IDictionary<string, string> expressions)
        {
            string[] categories =
            {
                "greeting", "dizzy", "cpu", "click", "angry", "idle", "squeeze"
            };
            for (int i = 0; i < categories.Length; i++)
            {
                SpeechLine[] lines;
                if (!speech.TryGetValue(categories[i], out lines) ||
                    lines == null ||
                    lines.Length == 0)
                {
                    throw new InvalidDataException("對話 JSON 缺少有效的類別：" + categories[i]);
                }

                for (int line = 0; line < lines.Length; line++)
                {
                    string expression;
                    if (lines[line] == null ||
                        String.IsNullOrWhiteSpace(lines[line].Text) ||
                        String.IsNullOrWhiteSpace(lines[line].Expression) ||
                        !expressions.TryGetValue(lines[line].Expression, out expression) ||
                        String.IsNullOrWhiteSpace(expression))
                    {
                        throw new InvalidDataException("對話或對應表情無效：" + categories[i]);
                    }
                }
            }
        }

        private static void ValidateExpressions(IDictionary<string, string> expressions)
        {
            string[] names =
            {
                "normal", "happy", "dizzy", "cpu", "surprised", "worried",
                "shy", "laughing", "proud", "curious", "playful", "mischievous",
                "thoughtful", "warm", "sleepy", "angry", "thinking", "relaxed",
                "lonely", "dreamy", "excited"
            };
            for (int i = 0; i < names.Length; i++)
            {
                string expression;
                if (!expressions.TryGetValue(names[i], out expression) ||
                    String.IsNullOrWhiteSpace(expression))
                {
                    throw new InvalidDataException("表情 JSON 缺少有效的表情：" + names[i]);
                }
            }
        }
    }
}
