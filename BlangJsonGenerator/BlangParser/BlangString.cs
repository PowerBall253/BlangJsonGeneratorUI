// Taken from BlangParser by proteh
// https://github.com/dcealopez/BlangParser

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BlangParser
{
    /// <summary>
    /// BlangString class
    /// </summary>
    public class BlangString : INotifyPropertyChanged
    {
        /// <summary>
        /// PropertyModified event
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Notifies if a property was changed
        /// </summary>
        /// <param name="propertyName">name of the changed property</param>
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")  
        {  
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }  

        /// <summary>
        /// The string's hash
        /// </summary>
        public uint Hash { get; set; }

        /// <summary>
        /// String identifier
        /// </summary>
        public string Identifier { get; set; }

        /// <summary>
        /// Original string identifier
        /// </summary>
        public string OriginalIdentifier { get; }

        /// <summary>
        /// The string's text
        /// </summary>
        private string _text { get; set; }

        /// <summary>
        /// Text property that notifies when value is changed
        /// </summary>
        public string Text
        {
            get => _text;

            set
            {
                if (value != _text)
                {
                    _text = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The original string's text
        /// </summary>
        public string OriginalText { get; }

        /// <summary>
        /// Unknown string data
        /// </summary>
        public string Unknown { get; set; }

        /// <summary>
        /// Indicates if the string was modified
        /// </summary>
        private bool _modified;

        /// <summary>
        /// Modified property that notifies when value is changed
        /// </summary>
        public bool Modified
        {
            get => _modified;

            set
            {
                if (value != _modified)
                {
                    _modified = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// BlangString default constructor
        /// </summary>
        public BlangString()
        {
            Hash = 0;
            Identifier = "";
            OriginalIdentifier = "";
            Text = "";
            OriginalText = "";
            Unknown = "";
            Modified = false;
        }

        /// <summary>
        /// BlangString constructor
        /// </summary>
        /// <param name="hash">the string's hash</param>
        /// <param name="identifier">the string's identifier</param>
        /// <param name="originalIdentifier">the original string identifier</param>
        /// <param name="text">the string's text</param>
        /// <param name="originalText">the original string text</param>
        /// <param name="unknown">unknown value</param>
        /// <param name="modified">indicates if the string has been modified</param>
        public BlangString(uint hash, string identifier, string originalIdentifier, string text, string originalText, string unknown, bool modified)
        {
            Hash = hash;
            Identifier = identifier;
            OriginalIdentifier = originalIdentifier;
            Text = text;
            OriginalText = originalText;
            Unknown = unknown;
            Modified = modified;
        }
    }
}
