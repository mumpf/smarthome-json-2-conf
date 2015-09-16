using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace conf2json {
    public class JPropertyComment : JProperty {
        //string mComment = null;
        public JPropertyComment(string iName, object iContent, string iComment) : base(iName, iContent) {
            Comment = iComment;
        }

        public string Comment { get; private set; }

        public bool WithComment {
            get {
                return (Comment != null && Comment != "");
            }
        }

        public override void WriteTo(JsonWriter writer, params JsonConverter[] converters) {
            base.WriteTo(writer, converters);
            if (this.WithComment) writer.WriteComment(Comment);
        }
    }
    public class JObjectComment : JObject {
        List<string> mComment = null;
        public JObjectComment() : base() {
            mComment = new List<string>();
        }

        public bool WithComment {
            get {
                return (mComment.Count > 0);
            }
        }

        public void CommentAdd(string iComment) {
            mComment.Add(iComment);
        }

        /// <summary>
        /// Writes this token to a <see cref="JsonWriter"/>.
        /// </summary>
        /// <param name="writer">A <see cref="JsonWriter"/> into which this method will write.</param>
        /// <param name="converters">A collection of <see cref="JsonConverter"/> which will be used when writing the token.</param>
        public override void WriteTo(JsonWriter writer, params JsonConverter[] converters) {
            writer.WriteStartObject();
            if (this.WithComment) {
                var lPath = writer.Path;
                var lState = writer.WriteState;
                StringBuilder lComments = new StringBuilder();
                int lLevel = (lPath.Split('.').Length + 1) * 2;
                if (lPath == "") lLevel = 2;
                writer.WriteWhitespace("\r\n");
                for (int lCount = 0; lCount < mComment.Count; lCount++) {
                    if (lLevel > 0) writer.WriteWhitespace(new string(' ', lLevel));
                    writer.WriteRaw("//" + mComment[lCount]);
                    if (lCount + 1 < mComment.Count) writer.WriteWhitespace("\r\n");
                }
            }
            var _properties = base.Properties();
            foreach (var lProperty in _properties) {
                lProperty.WriteTo(writer, converters);
            }
            writer.WriteEndObject();


            //base.WriteTo(writer, converters);
        }
    }
}
