using System.Collections.Concurrent;
using Ipfs;
using Ipfs.Http;
using Microsoft.Extensions.Logging;
using PocketSizedUniverse.Config;
using PocketSizedUniverse.Data;
using PocketSizedUniverse.Services;
using PocketSizedUniverse.Util;

namespace PocketSizedUniverse.Controllers;

public class ChatController : IDisposable
{
    private readonly ILogger<ChatController> _logger;
    private readonly Configuration _configuration;
    private readonly IpfsService _ipfsService;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _messageProcessingCts = new();
    public ConcurrentDictionary<Guid, Dictionary<Guid, ChatMessage>> ChatMessages { get; } = new();

    public ChatController(ILogger<ChatController> logger, Configuration configuration, IpfsService ipfsService)
    {
        _logger = logger;
        _configuration = configuration;
        _ipfsService = ipfsService;
        Task.Run(() => DoChatWork(_cts.Token), _cts.Token);
    }

    private async Task DoChatWork(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var subbedTopics = await _ipfsService.GetSubscribedTopics();
                foreach (var chat in _configuration.Chats)
                {
                    var topic = TopicUtil.GetChatTopic(chat);
                    if (subbedTopics.Contains(topic)) continue;
                    var subCts = new CancellationTokenSource();
                    await _ipfsService.SubscribeToTopic(topic, HandleChatMessage, subCts.Token);
                    _messageProcessingCts[chat] = subCts;
                }

                foreach (var subbedChat in subbedTopics)
                {
                    if (!TopicUtil.IsChatTopic(subbedChat)) continue;
                    var guid = TopicUtil.TopicToPairedGuid(subbedChat);
                    if (guid == null || _configuration.Chats.Contains(guid.Value)) continue;
                    if (!_messageProcessingCts.TryGetValue(guid.Value, out var cts)) continue;
                    await cts.CancelAsync();
                    _messageProcessingCts.TryRemove(guid.Value, out _);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing chat messages");
            }
            finally
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
    }

    public async Task SendChatMessage(Guid chatId, string message)
    {
        var messageObj = new ChatMessage()
        {
            ChatId = chatId,
            MessageId = Guid.NewGuid(),
            SenderId = _configuration.PairingId,
            Timestamp = DateTime.UtcNow,
            Message = message
        };
        var topic = TopicUtil.GetChatTopic(chatId);
        var base64Text = Base64Util.ToBase64(messageObj);
        await _ipfsService.PublishToTopic(topic, base64Text, _cts.Token);
        var existingChat = ChatMessages.GetOrAdd(chatId, new Dictionary<Guid, ChatMessage>());
        existingChat[messageObj.MessageId] = messageObj;
    }

    private void HandleChatMessage(IPublishedMessage message)
    {
        if (message is not PublishedMessage publishedMessage)
            return;
        var json = publishedMessage.DataString;
        var dataObj = Base64Util.FromBase64<ChatMessage>(json);
        if (dataObj is null)
            return;

        var chatId = dataObj.ChatId;
        var messageId = dataObj.MessageId;
        
        ChatMessages.AddOrUpdate(chatId, new Dictionary<Guid, ChatMessage> { { messageId, dataObj } }, (key, value) =>
        {
            value[messageId] = dataObj;
            return value;
        });
    }

    public void Dispose()
    {
        foreach (var (_, cts) in _messageProcessingCts)
        {
            cts.Cancel();
        }
        _cts.Dispose();
    }
}