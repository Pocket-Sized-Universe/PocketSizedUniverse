using Newtonsoft.Json;

namespace Syncthing.Models.Response
{
    public enum FolderType
    {
        SendReceive,
        SendOnly,
        ReceiveEncrypted,
        ReceiveOnly
    }

    public static class FolderTypeExtensions
    {
        public static string ToApiString(this FolderType folderType)
        {
            return folderType switch
            {
                FolderType.SendReceive => "sendreceive",
                FolderType.SendOnly => "sendonly",
                FolderType.ReceiveOnly => "receiveonly",
                FolderType.ReceiveEncrypted => "receiveencrypted",
                _ => throw new ArgumentOutOfRangeException(nameof(folderType), folderType, null)
            };
        }

        public static FolderType FromApiString(this string folderTypeString)
        {
            return folderTypeString switch
            {
                "sendreceive" => FolderType.SendReceive,
                "sendonly" => FolderType.SendOnly,
                "receiveonly" => FolderType.ReceiveOnly,
                "receiveencrypted" => FolderType.ReceiveEncrypted,
                _ => throw new ArgumentOutOfRangeException(nameof(folderTypeString), folderTypeString, null)
            };
        }
    }
}