/* ====================================================================
   Licensed to the Apache Software Foundation (ASF) under one or more
   contributor license agreements.  See the NOTICE file distributed with
   this work for Additional information regarding copyright ownership.
   The ASF licenses this file to You under the Apache License, Version 2.0
   (the "License"); you may not use this file except in compliance with
   the License.  You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
==================================================================== */

namespace NPOI
{
    using System;
    using System.IO;
    using System.Collections;
    using NPOI.POIFS.FileSystem;
    using NPOI.HPSF;
    using System.Collections.Generic;
    using NPOI.POIFS.Crypt;
    using NPOI.Util;


    /// <summary>
    /// This holds the common functionality for all POI
    /// Document classes.
    /// Currently, this relates to Document Information Properties
    /// </summary>
    /// <remarks>@author Nick Burch</remarks>
    [Serializable]
    public abstract class POIDocument
    {
        /** Holds metadata on our document */
        protected SummaryInformation sInf;
        /** Holds further metadata on our document */
        protected DocumentSummaryInformation dsInf;
        /**	The directory that our document lives in */
        protected DirectoryNode directory;

        /// <summary>
        /// just for test case  TestPOIDocumentMain.TestWriteReadProperties
        /// </summary>
        protected internal void SetDirectoryNode(DirectoryNode directory)
        {
            this.directory = directory;
        }

        /** For our own logging use */
        //protected POILogger logger;

        /* Have the property streams been Read yet? (Only done on-demand) */
        protected bool initialized = false;

        protected POIDocument(DirectoryNode dir)
        {
            this.directory = dir;
        }
        /**
     * Constructs from an old-style OPOIFS
     */
        protected POIDocument(OPOIFSFileSystem fs)
            : this(fs.Root)
        {
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="POIDocument"/> class.
        /// </summary>
        /// <param name="fs">The fs.</param>
        public POIDocument(NPOIFSFileSystem fs)
            : this(fs.Root) 
        {
            
        }
        /**
     * Constructs from the default POIFS
     */
        protected POIDocument(POIFSFileSystem fs)
            : this(fs.Root)
        {
        }
        /**
         * Will create whichever of SummaryInformation
         *  and DocumentSummaryInformation (HPSF) properties
         *  are not already part of your document.
         * This is normally useful when creating a new
         *  document from scratch.
         * If the information properties are already there,
         *  then nothing will happen.
         */
        public void CreateInformationProperties()
        {
            if (!initialized) ReadProperties();
            if (sInf == null)
            {
                sInf = PropertySetFactory.CreateSummaryInformation();
            }
            if (dsInf == null)
            {
                dsInf = PropertySetFactory.CreateDocumentSummaryInformation();
            }
        }
        // nothing to dispose
        //public virtual void Dispose()
        //{
        //
        //}
        /// <summary>
        /// Fetch the Document Summary Information of the document
        /// </summary>
        /// <value>The document summary information.</value>
        public DocumentSummaryInformation DocumentSummaryInformation
        {
            get
            {
                if (!initialized) ReadProperties();
                return dsInf;
            }
            set 
            {
                dsInf = value;
            }
        }

        /// <summary>
        /// Fetch the Summary Information of the document
        /// </summary>
        /// <value>The summary information.</value>
        public SummaryInformation SummaryInformation
        {
            get
            {
                if (!initialized) ReadProperties();
                return sInf;
            }
            set 
            {
                sInf = value;
            }
        }

        /// <summary>
        /// Find, and Create objects for, the standard
        /// Documment Information Properties (HPSF).
        /// If a given property Set is missing or corrupt,
        /// it will remain null;
        /// </summary>
        protected internal void ReadProperties()
        {
            PropertySet ps;

            // DocumentSummaryInformation
            ps = GetPropertySet(DocumentSummaryInformation.DEFAULT_STREAM_NAME);
            if (ps != null && ps is DocumentSummaryInformation)
            {
                dsInf = (DocumentSummaryInformation)ps;
            }
            else if (ps != null)
            {
                //logger.Log(POILogger.WARN, "DocumentSummaryInformation property Set came back with wrong class - ", ps.GetType());
            }

            // SummaryInformation
            ps = GetPropertySet(SummaryInformation.DEFAULT_STREAM_NAME);
            if (ps is SummaryInformation)
            {
                sInf = (SummaryInformation)ps;
            }
            else if (ps != null)
            {
                //logger.Log(POILogger.WARN, "SummaryInformation property Set came back with wrong class - ", ps.GetType());
            }

            // Mark the fact that we've now loaded up the properties
            initialized = true;
        }
        /// <summary>
        /// For a given named property entry, either return it or null if
        /// if it wasn't found
        /// </summary>
        /// <param name="setName">The property to read</param>
        /// <returns>The value of the given property or null if it wasn't found.</returns>
        /// <exception cref="IOException">If retrieving properties fails</exception>
        protected PropertySet GetPropertySet(string setName)
        {
            return GetPropertySet(setName, null);
        }
        /// <summary>
        /// For a given named property entry, either return it or null if
        /// if it wasn't found
        /// </summary>
        /// <param name="setName">The property to read</param>
        /// <param name="encryptionInfo">the encryption descriptor in case of cryptoAPI encryption</param>
        /// <returns>The value of the given property or null if it wasn't found.</returns>
        /// <exception cref="IOException">If retrieving properties fails</exception>
        protected PropertySet GetPropertySet(string setName, EncryptionInfo encryptionInfo)
        {
            DirectoryNode dirNode = directory;

            NPOIFSFileSystem encPoifs = null;
            if (encryptionInfo != null)
            {
                try
                {
                    InputStream is1 = encryptionInfo.Decryptor.GetDataStream(directory);
                    encPoifs = new NPOIFSFileSystem(is1);
                    is1.Close();
                    dirNode = encPoifs.Root;
                }
                catch (Exception e)
                {
                    //logger.log(POILogger.ERROR, "Error getting encrypted property set with name " + setName, e);
                    return null;
                }
            }
            //directory can be null when creating new documents
            if (dirNode == null || !dirNode.HasEntry(setName))
                return null;

            DocumentInputStream dis;
            try
            {
                // Find the entry, and Get an input stream for it
                dis = directory.CreateDocumentInputStream(setName);
            }
            catch (IOException)
            {
                // Oh well, doesn't exist
                //logger.Log(POILogger.WARN, "Error Getting property Set with name " + SetName + "\n" + ie);
                return null;
            }

            try
            {
                // Create the Property Set
                PropertySet Set = PropertySetFactory.Create(dis);
                // Tidy up if needed
                if (encPoifs != null)
                {
                    encPoifs.Close();
                }
                // Return the properties
                return Set;
            }
            catch (IOException)
            {
                // Must be corrupt or something like that
                //logger.Log(POILogger.WARN, "Error creating property Set with name " + SetName + "\n" + ie);
            }
            catch (HPSFException)
            {
                // Oh well, doesn't exist
                //logger.Log(POILogger.WARN, "Error creating property Set with name " + SetName + "\n" + he);
            }
            return null;
        }
        /**
         * Writes out the updated standard Document Information Properties (HPSF)
         *  into the currently open NPOIFSFileSystem
         * TODO Implement in-place update
         * 
         * @throws IOException if an error when writing to the open
         *      {@link NPOIFSFileSystem} occurs
         * TODO throws exception if open from stream not file
         */
        protected internal void WriteProperties()
        {
            throw new InvalidOperationException("In-place write is not yet supported");
        }
        /// <summary>
        /// Writes out the standard Documment Information Properties (HPSF)
        /// </summary>
        /// <param name="outFS">the POIFSFileSystem to Write the properties into</param>
        protected internal void WriteProperties(NPOIFSFileSystem outFS)
        {
            WriteProperties(outFS, null);
        }
        /// <summary>
        /// Writes out the standard Documment Information Properties (HPSF)
        /// </summary>
        /// <param name="outFS">the POIFSFileSystem to Write the properties into.</param>
        /// <param name="writtenEntries">a list of POIFS entries to Add the property names too.</param>
        protected internal void WriteProperties(NPOIFSFileSystem outFS, IList writtenEntries)
        {
            if (sInf != null)
            {
                WritePropertySet(SummaryInformation.DEFAULT_STREAM_NAME, sInf, outFS);
                if (writtenEntries != null)
                {
                    writtenEntries.Add(SummaryInformation.DEFAULT_STREAM_NAME);
                }
            }
            if (dsInf != null)
            {
                WritePropertySet(DocumentSummaryInformation.DEFAULT_STREAM_NAME, dsInf, outFS);
                if (writtenEntries != null)
                {
                    writtenEntries.Add(DocumentSummaryInformation.DEFAULT_STREAM_NAME);
                }
            }
        }

        /// <summary>
        /// Writes out a given ProperySet
        /// </summary>
        /// <param name="name">the (POIFS Level) name of the property to Write.</param>
        /// <param name="Set">the PropertySet to Write out.</param>
        /// <param name="outFS">the POIFSFileSystem to Write the property into.</param>
        protected void WritePropertySet(String name, PropertySet Set, NPOIFSFileSystem outFS)
        {
            try
            {
                MutablePropertySet mSet = new MutablePropertySet(Set);
                using (MemoryStream bOut = new MemoryStream())
                {
                    mSet.Write(bOut);
                    byte[] data = bOut.ToArray();
                    using (MemoryStream bIn = new MemoryStream(data))
                    {
                        outFS.CreateDocument(bIn, name);
                    }
                    //logger.Log(POILogger.INFO, "Wrote property Set " + name + " of size " + data.Length);
                }
            }
            catch (WritingNotSupportedException)
            {
                //logger.log(POILogger.ERROR, "Couldn't Write property Set with name " + name + " as not supported by HPSF yet");
            }
        }

        /// <summary>
        /// Writes the document out to the specified output stream. The
        /// stream is not closed as part of this operation.
        /// </summary>
        /// <param name="out1">The stream to write to.</param>
        public abstract void Write(Stream out1);
    }
}