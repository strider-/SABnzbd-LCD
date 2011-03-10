using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Dynamic;

namespace SABnzbd_LCD {
    /// <summary>
    /// Represents data in JSON format.
    /// </summary>
    public class JsonDocument {
        enum JsonToken {
            Unknown = -1,
            OpenBrace,
            CloseBrace,
            OpenBracket,
            CloseBracket,
            Colon,
            String,
            Number,
            True,
            False,
            Null,
            Comma,
        }
        /// <summary>
        /// Indentation style for generated JSON documents.
        /// </summary>
        public enum JsonFormat {
            /// <summary>
            /// No identation or line breaks will be used.
            /// </summary>
            None,
            /// <summary>
            /// Indentation will be set with tab characters.
            /// </summary>
            Tabs,
            /// <summary>
            /// Indentation will be set with spaces.
            /// </summary>
            Spaces
        }

        string newLine;
        int parseIndex;
        char[] json;

        /// <summary>
        /// Initializes a new JSON document with the specified anonymous type or array content.
        /// </summary>
        /// <param name="content">An anonymous type or array to represent as JSON</param>
        public JsonDocument(object content)
            : this(content, JsonFormat.Tabs, 4) {
        }
        /// <summary>
        /// Initializes a new JSON document with the specified anonymous type or array content and formatting options.
        /// </summary>
        /// <param name="content">An anonymous type or array to represent as JSON</param>
        /// <param name="Formatting">Specifies the formatting of the generated JSON document</param>
        /// <param name="IndentSize">Indentation size when using spaces.</param>
        public JsonDocument(object content, JsonFormat Formatting, int IndentSize) {
            /*  Well this was a stupid restriction, wasn't it?
             * 
            if(content != null && !content.GetType().IsGenericType && !isArray(content))
                throw new ArgumentException("JsonDocument only works with anonymous types and arrays", "content");
            */

            if(Formatting == JsonFormat.Spaces && IndentSize < 0)
                throw new ArgumentException("Indentation size cannot be less than zero when using spaces.", "IndentSize");

            this.Content = content;

            this.IndentSize = IndentSize;
            this.Formatting = Formatting;
        }
        JsonDocument() {
        }
        bool isArray(object obj) {
            return obj is ICollection || obj is IEnumerator;
        }
        string tabString(int count) {
            if(Formatting == JsonFormat.Tabs)
                return new string('\t', count);
            else if(Formatting == JsonFormat.Spaces)
                return new string(' ', count * IndentSize);

            return string.Empty;
        }
        string clean(string value) {
            return Regex.Replace(value, "[\b\r\n\t\f\"\\/\\\\]", delegate(Match m) {
                switch(m.Value) {
                    case "\b":
                        return "\\b";
                    case "\r":
                        return "\\r";
                    case "\n":
                        return "\\n";
                    case "\t":
                        return "\\t";
                    case "\f":
                        return "\\f";
                    case "/":
                    case "\\":
                    case "\"":
                        return string.Format("\\{0}", m.Value);
                    default:
                        return m.Value;
                }
            });
        }

        string getJsonValue(object o, int tabCount) {
            if(o == null || (o.GetType().IsPrimitive && !(o is char)))
                return o == null ? "null" : o.ToString().ToLower();
            else if(isArray(o)) {
                return getJsonArray((IEnumerable)o, tabCount + 1);
            } else if(o is string || o is char) {
                return string.Format("\"{0}\"", clean(o.ToString()));
            } else {
                return getJsonObject(o, tabCount + 1);
            }
        }
        string getJsonArray(IEnumerable array, int tabCount) {
            StringBuilder sb = new StringBuilder();
            List<string> l = new List<string>();
            string tab = tabString(tabCount);

            sb.AppendFormat("[{0}{1}", newLine, tab);
            foreach(var item in array) {
                l.Add(getJsonValue(item, tabCount));
            }

            sb.Append(string.Join(string.Format(",{0}{1}", newLine, tab), l.ToArray()));
            sb.AppendFormat("{0}{1}]", newLine, tabString(tabCount - 1));

            return sb.ToString();
        }
        string getJsonObject(object obj, int tabCount) {
            StringBuilder sb = new StringBuilder();
            string tab = tabString(tabCount);

            sb.AppendFormat("{{{0}", newLine);

            if(obj != null) {
                PropertyInfo[] pi = obj.GetType().GetProperties();
                for(int i = 0; i < pi.Length; i++) {
                    var value = pi[i].GetValue(obj, null);
                    sb.AppendFormat("{0}\"{1}\": ", tab, pi[i].Name);
                    sb.Append(getJsonValue(value, tabCount));
                    sb.Append(i < pi.Length - 1 ? "," : "");
                    sb.Append(newLine);
                }
            }

            sb.AppendFormat("{0}}}", tabString(tabCount - 1));

            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON document.
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            if(Formatting == JsonFormat.Spaces && IndentSize < 0)
                throw new ArgumentException("Indentation size cannot be less than zero.", "IndentSize");

            newLine = Formatting == JsonFormat.None ? string.Empty : Environment.NewLine;

            if(Content == null) {
                return getJsonObject(Content, 1);
            } else if(Content.GetType().IsPrimitive || Content is string) {
                Content = new {
                    Value = Content
                };
            }

            return getJsonValue(Content, 0);
        }

        /// <summary>
        /// Parses a JSON document into a JsonDynamicDocument
        /// </summary>
        /// <param name="JsonObject">The JSON string to parse</param>
        /// <returns></returns>
        public static dynamic Parse(string JsonObject) {
            return new JsonDocument().ParseJson(JsonObject);
        }
        dynamic ParseJson(string jsonObject) {
            json = jsonObject.ToCharArray();
            parseIndex = 0;

            if(PeekToken() == JsonToken.OpenBrace)
                return new JsonDynamicDocument(parseObject());
            else if(PeekToken() == JsonToken.OpenBracket)
                return new JsonDynamicDocument(parseArray());

            throw new Exception("Invalid JSON document.");
        }
        void consumeWhiteSpace() {
            while(json.Length > parseIndex && "\r\n\t ".IndexOf(json[parseIndex]) != -1)
                parseIndex++;
        }
        JsonToken NextToken() {
            consumeWhiteSpace();

            if(parseIndex == json.Length)
                return JsonToken.Unknown;

            switch(json[parseIndex++]) {
                case '{':
                    return JsonToken.OpenBrace;
                case '}':
                    return JsonToken.CloseBrace;
                case '[':
                    return JsonToken.OpenBracket;
                case ']':
                    return JsonToken.CloseBracket;
                case ':':
                    return JsonToken.Colon;
                case '"':
                    return JsonToken.String;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '+':
                case '-':
                    return JsonToken.Number;
                case ',':
                    return JsonToken.Comma;
            }

            int remLen = json.Length - parseIndex;
            if(remLen >= 5 && new string(json, parseIndex - 1, 5).ToLower() == "false") {
                return JsonToken.False;
            } else {
                if(remLen >= 4) {
                    string tmp = new string(json, parseIndex - 1, 4).ToLower();
                    if(tmp == "true")
                        return JsonToken.True;
                    else if(tmp == "null")
                        return JsonToken.Null;
                }
            }

            return JsonToken.Unknown;
        }
        JsonToken PeekToken() {
            int curIndex = parseIndex;
            JsonToken token = NextToken();
            parseIndex = curIndex;
            return token;
        }
        Hashtable parseObject() {
            if(NextToken() == JsonToken.OpenBrace) {
                Hashtable table = new Hashtable();
                JsonToken peek;

                while((peek = PeekToken()) != JsonToken.CloseBrace) {
                    switch(peek) {
                        case JsonToken.String:
                            string name = parseString();
                            object value = null;
                            if(NextToken() == JsonToken.Colon)
                                value = parseValue();
                            table[name] = value;
                            break;
                        case JsonToken.Comma:
                            NextToken();
                            break;
                        default:
                            throw parseException();
                    }
                }

                // Consume closing brace (})
                NextToken();
                return table;
            }

            throw parseException();
        }
        object parseValue() {
            consumeWhiteSpace();

            switch(PeekToken()) {
                case JsonToken.String:
                    return parseString();
                case JsonToken.Number:
                    return parseNumber();
                case JsonToken.True:
                    parseIndex += 4;
                    return true;
                case JsonToken.False:
                    parseIndex += 5;
                    return false;
                case JsonToken.OpenBrace:
                    return parseObject();
                case JsonToken.OpenBracket:
                    return parseArray();
                case JsonToken.Null:
                    parseIndex += 4;
                    return null;
                case JsonToken.Unknown:
                    throw parseException();
                default:
                    return null;
            }
        }
        ArrayList parseArray() {
            ArrayList array = new ArrayList();

            // Consume opening bracket ([)
            NextToken();

            JsonToken peek;
            while((peek = PeekToken()) != JsonToken.CloseBracket) {
                if(peek == JsonToken.Comma) {
                    NextToken();
                } else if(peek == JsonToken.Unknown) {
                    throw parseException();
                } else {
                    array.Add(parseValue());
                }
            }

            // Consume closing bracket (])
            NextToken();
            return array;
        }
        string parseString() {
            StringBuilder sb = new StringBuilder();
            bool inString = true;

            // Consume opening double quote (")
            NextToken();

            while(inString) {
                if(parseIndex == json.Length)
                    throw parseException();

                char c = json[parseIndex++];
                if(c == '"')
                    inString = false;
                else if(c == '\\') {
                    c = json[parseIndex++];
                    switch(c) {
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'u':
                            char[] num = new char[4];
                            Array.Copy(json, parseIndex, num, 0, 4);
                            int charValue = int.Parse(new string(num), System.Globalization.NumberStyles.HexNumber);
                            sb.Append(Char.ConvertFromUtf32(charValue));
                            parseIndex += 4;
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                } else {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
        double parseNumber() {
            int curIndex = parseIndex;
            while(".-+eE01234567890".IndexOf(json[parseIndex]) != -1)
                parseIndex++;
            int len = parseIndex - curIndex;

            char[] num = new char[len];
            Array.Copy(json, curIndex, num, 0, len);

            return double.Parse(new string(num));
        }
        Exception parseException() {
            if(parseIndex == json.Length)
                return parseException("Unexpected end of JSON document.");

            return parseException("Unexpected token '" + json[parseIndex] + "'");
        }
        Exception parseException(string msg) {
            consumeWhiteSpace();
            return new Exception(msg + "\r\nCharacter position: " + parseIndex.ToString());
        }

        /// <summary>
        /// Gets and sets the formatting to use when generating JSON documents.
        /// </summary>
        public JsonFormat Formatting {
            get;
            set;
        }
        /// <summary>
        /// Gets and sets the indentation size for formatting.  Only used when Formatting is JsonFormat.Spaces.
        /// </summary>
        public int IndentSize {
            get;
            set;
        }
        object Content {
            get;
            set;
        }
    }

    public class JsonDynamicDocument : DynamicObject {
        object obj;
        Type objType;

        internal JsonDynamicDocument(object jsonObject) {
            this.obj = jsonObject;
            IsHashtable = obj is Hashtable;
            IsArrayList = obj is ArrayList;
            objType = obj.GetType();
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            object o = IsHashtable ? ((Hashtable)obj)[binder.Name] : null;

            if(o != null) {
                if(o is Hashtable || o is ArrayList)
                    result = new JsonDynamicDocument(o);
                else
                    result = o;
            } else {
                // invoke the member on the actual object, if it exists.
                MemberInfo[] mi = objType.GetMember(binder.Name);
                if(mi != null && mi.Length > 0)
                    result = objType.InvokeMember(binder.Name, BindingFlags.GetProperty | BindingFlags.GetField, null, obj, null);
                else
                    result = null;
            }
            
            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            // invoke the method on the actual object
            MethodInfo mi = objType.GetMethod(binder.Name);
            if(mi != null)
                result = mi.Invoke(obj, args);
            else
                result = null;
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            if(IsArrayList) {
                ArrayList al = obj as ArrayList;
                result = al[(int)indexes[0]];
                if(result is Hashtable)
                    result = new JsonDynamicDocument(result);
            } else {
                result = null;
            }

            return true;
        }

        public bool IsHashtable {
            get;
            private set;
        }
        public bool IsArrayList {
            get;
            private set;
        }
    }
}
