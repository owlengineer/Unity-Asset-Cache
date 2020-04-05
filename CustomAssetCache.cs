using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace IAssetCacheJB
{
    public static class UnityHelper
    {
        public static int MB = 1024 * 1024;
        public static int chunk_2MB = 2 * MB;
        public static int chunk_4MB = 4 * MB;
        public static int chunk_8MB = 8 * MB;
    }

    public class FilterState
    {
        public ulong mLastGameObjectId = 0;
        public bool mComponents = false;
    }
    public class CacheState
    {
        public int countBytesRead { get; private set; }
        public int countBytesToRead { get; private set; }
        public int currentChunkSize { get; private set; }

        public CacheState(int fileLength, int chunkSize)
        {
            countBytesRead = 0;
            countBytesToRead = fileLength;
            currentChunkSize = chunkSize;
        }

        public bool lastChunk()
        {
            return countBytesToRead < currentChunkSize;
        }

        public void carveBufferToLastChunkSize()
        {
            currentChunkSize = countBytesToRead;
        } 

        public void recalcState()
        {
            
            countBytesRead += currentChunkSize;
            countBytesToRead -= currentChunkSize;
        }
    }
    public class CustomAssetCache : IAssetCache
    {
        // constants
        private const string anchor_ID = "--- !u!1 &";
        private const string fileIDStr = "fileID: ";
        private const string guid_str = "guid: ";
        
        // cache fields
        private FileStream mFileStream;
        private byte[] mData;
        private CacheState mState;
        private FileInfo mFileInfo;
        
        private Dictionary<ulong, int> mAnchorUses;
        private Dictionary<string, int> mResourcesUses;
        private Dictionary<ulong, LinkedList<ulong>> mGameObjectComponents;
        
        // filter helper
        private FilterState mFilterState;
        
        public CustomAssetCache()
        {
            mFileStream = null;
            mData = null;
            mState = null;
            mFileInfo = null;
            mAnchorUses = new Dictionary<ulong, int>();
            mResourcesUses = new Dictionary<string, int>();
            mGameObjectComponents = new Dictionary<ulong, LinkedList<ulong>>();
            mFilterState = new FilterState();
        }

        public bool AssetFileChanged(string path)
        {
            FileInfo currentFileInfo = new FileInfo(path);
            return mFileInfo.LastWriteTime.ToString() != currentFileInfo.LastWriteTime.ToString();
        }

        public bool IsInitialized()
        {
            return !(mFileStream == null && mData == null && mState == null);
        }

        public void Initialize(string path)
        {
            mFileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            mFileInfo = new FileInfo(path);
            mData = new byte[mFileStream.Length];
            mState = new CacheState((int)mFileStream.Length, UnityHelper.chunk_2MB);
        }
        public object Build(string path, Action interruptChecker)

        {
            // building cache
            if (!IsInitialized())
                Initialize(path);

            if (AssetFileChanged(path))
            {
                Thread.Sleep(2500);
                Initialize(path);
            } 
            
            while (mState.countBytesToRead > 0)
            {
                interruptChecker();

                if(mState.lastChunk())
                    mState.carveBufferToLastChunkSize();
                
                int n = mFileStream.Read(mData, mState.countBytesRead, mState.currentChunkSize);
                mState.recalcState();
                
                Thread.Sleep(10);
            }
            return mData;
        }

        private ulong GetFileIDFromLine(string str, int beginIndex)
        {
            ulong id = 0;
            for (int i = beginIndex; i < str.Length; i++)
            {
                if (str[i] == ',' || str[i] == '}')
                {
                    id = Convert.ToUInt64(str.Substring(beginIndex, i-beginIndex));
                    break;
                }       
            }
            return id;
        }
        
        private string GetGuidFromLine(string str, int beginIndex)
        {
            string guid = "";
            for (int i = beginIndex; i < str.Length; i++)
            {
                if (str[i] == ',' || str[i] == '}')
                {
                    guid = str.Substring(beginIndex, i-beginIndex);
                    break;
                }       
            }
            return guid;
        }

        private void AppendIdDataIfExists(string prefix, string line)
        {
            if (line.Contains("m_Component"))
            {
                mFilterState.mComponents = true;
                return;
            }
            int index_fileID = line.IndexOf(prefix, StringComparison.Ordinal);
            if (index_fileID != -1)
            {
                int val = 0;
                if (prefix.ToString() == fileIDStr.ToString())
                {
                    ulong id = GetFileIDFromLine(line, index_fileID+prefix.Length);
                    mAnchorUses.TryGetValue(id, out val);
                    mAnchorUses[id] = val + 1;
                    if (mFilterState.mComponents)
                    {
                        LinkedList<ulong> l;
                        mGameObjectComponents.TryGetValue(mFilterState.mLastGameObjectId, out l);
                        if (l == null)
                        {
                            l = new LinkedList<ulong>();
                            mGameObjectComponents[mFilterState.mLastGameObjectId] = l;
                        }
                        mGameObjectComponents[mFilterState.mLastGameObjectId].AddLast(id);
                    }
                }
                else
                { 
                    string id = GetGuidFromLine(line, index_fileID+prefix.Length);
                    mResourcesUses.TryGetValue(id, out val);
                    mResourcesUses[id] = val + 1;
                }
            }
            else if(index_fileID == -1 && prefix.ToString() == fileIDStr)
            {
                mFilterState.mComponents = false;
            }
        }

        private ulong? GetNewGameObjectIdIfExists(string line)
        {
            if (line.Contains(anchor_ID))
            {
                return Convert.ToUInt64(line.Substring(anchor_ID.Length));
            }
            else
                return null;
        }
        
        public void Merge(string path, object result)
        {
            string str = Encoding.UTF8.GetString((byte[]) Convert.ChangeType(result, typeof(byte[])));
            
            StringReader reader = new StringReader(str);

            int i = 0;
            var line = reader.ReadLine();
            while (line != null)
            {
                ulong? tmp_id = GetNewGameObjectIdIfExists(line);
                if (tmp_id != null)
                    mFilterState.mLastGameObjectId = tmp_id.Value;
                
                // checking fileID in current Line && components
                AppendIdDataIfExists(fileIDStr, line);
                
                // checking guid in current line
                AppendIdDataIfExists(guid_str, line);

                line = reader.ReadLine();
                i++;
            }
        }

        public int GetLocalAnchorUsages(ulong anchor)
        {
            return mAnchorUses[anchor];
        }

        public int GetGuidUsages(string guid)
        {
            return mResourcesUses[guid];
        }

        public IEnumerable<ulong> GetComponentsFor(ulong gameObjectAnchor)
        {
            return mGameObjectComponents[gameObjectAnchor];
        }
    }
}