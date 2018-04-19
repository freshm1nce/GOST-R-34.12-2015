module Telegram

open AbstractChannel
open BotMediatorApi

open System
open Utility
open FSharp.Data
open System.Net
open System.Net.Http
open System.Web.Http
open global.Owin
open Microsoft.Owin
open Microsoft.Owin.Extensions
open System.Runtime.Serialization
open Newtonsoft.Json
open System.Net
open Utility
open System.Collections.Specialized
open System.Net.Http 
open System.IO
open Bots
open OwinHelper
open Log
open JsonUtility

// configure NLog
let log = NLog.LogManager.GetLogger("telegram")

let json_settings:JsonSerializerSettings = new JsonSerializerSettings() 
json_settings.NullValueHandling <- NullValueHandling.Ignore

type InlineKeyboardMarkup = {
    inline_keyboard: InlineKeyboardButton [][]
}
and InlineKeyboardButton = {
    text: string
    url: string
    callback_data: string
    //switch_inline_query: string
}

type ReplyKeyboardMarkup = {
    row_width: int
    keyboard: KeyboardButton [] []
    resize_keyboard: bool
    one_time_keyboard: bool
}
and KeyboardButton = {
    text: string
    request_contact: bool
    request_location: bool 
}

type Result = {
    ok: bool
    result: Update[]
}
and Update = {
    update_id: int
    message: Message
    edited_message: Message
    //    inline_query: InlineQuery
    //    chosen_inline_result: ChosenInlineResult
    callback_query: CallbackQuery
}
and Message = {
    message_id: int
    from: User
    date: int64
    chat: Chat
    //forward_from: User
    //forward_from_chat: Chat
    //forward_date: int
    //reply_to_message: Message
    //edit_date: int
    text: string
    //entities: MessageEntity []
    //audio: Audio
    document: Document
    photo: PhotoSize []
    //sticker: Sticker
    //video: Video
    //voice: Voice
    caption: string
    contact: Contact
    location: Location
    //venue: Venue
    //new_chat_member: User
    //left_chat_member: User
    //new_chat_title: string
    //new_chat_photo: PhotoSize[]
    //delete_chat_photo: bool
    //group_chat_created: bool
    //supergroup_chat_created: bool
}
and Location = {
    longitude: float
    latitude: float
}
and User = {
    id: int
    first_name: string
    last_name: string
    username: string
}
and PhotoSize = {
    file_id: string
    width: int
    height: int
    file_size: int
}
and Chat = {
    id: int

    [<field: DataMember(Name="type")>]
    chattype: string

    title: string
    username: string
    first_name: string
    last_name: string
}
and Contact = {   
    phone_number: string
    first_name: string
    last_name: string
    user_id: int
    username: string
}
and CallbackQuery = {
    id: string
    from: User
    message: Message
    inline_messag_id: string
    data: string 
}
and Document = {
    file_id: string
    thumb: PhotoSize
    file_name: string
    mime_type: string
    file_size: string
}

type FileResult = {
    ok: bool
    result: File
}
and File = {
    file_id: string;
    file_size: int;
    file_path: string;
}

type UserProfilePhotosResult = {
    ok: bool
    result: UserProfilePhotos    
} 
and UserProfilePhotos = {
    total_count: int
    photos: PhotoSize[][]
}
  
let mutable get_bot = fun (id:string) -> setNull<ChannelBot>()


let mutable uniqueBotToken = ""


let getEndpoint token path = "https://api.telegram.org/bot" + token + "/" + path

let sendMessage0 (token: string) (cid: string) (body: string) =
    let url  = getEndpoint token "sendMessage"
    try
        let res = Http.RequestString (url, query=["chat_id", cid; "text", body ]) //|> ignore 
        ()
    with
    |ex -> log.error "sendMessage0 %A" ex
           reraise()


let sendMessage (token: string) (cid: string) (body: string) 
                (replay_markup: InlineKeyboardMarkup option) = async {
    let url  = getEndpoint token "sendMessage"
    try
        let markup = 
            match replay_markup with 
            |Some markup -> JsonSerialize markup  
            |None -> "{\"hide_keyboard\": true}"    //JsonSerialize { keyboard = [| [| { text = "Отмена" } |] |]; resize_keyboard = true }   
        let! res = Http.AsyncRequestString (url, query=["chat_id", cid; "text", body; "reply_markup", markup ])
        ()
    with
    | ex -> 
        log.error "sendMessage %A" ex
}


let sendMessageSimple (token: string) (cid: string) (body: string) = sendMessage token cid body None

let requestLocation (token: string) (cid: string) (body: string) =
    let url  = getEndpoint token "sendMessage"
    try
        let res = Http.RequestString (url, query=["chat_id", cid; "text", body; "reply_markup", "{\"keyboard\": [[{\"text\": \"Нажми сюда для отправки контактных данных\", \"request_contact\": true}]], \"one_time_keyboard\": true}"  ]) 
        ()
    with
    | ex -> log.error "%A" ex
            reraise()

let sendPhoto (token: string) (cid: string) (caption:string) (photo: string) = async {
    let url  = getEndpoint token "sendPhoto"
    try
        let caption = if isNull(caption) then "" else caption
        let title = caption.Substring(0, min 200 caption.Length)
        if photo.StartsWith("local:") then
            let fname = photo.Replace("local:", "")
            let client = new HttpClient()
            let form = new MultipartFormDataContent()
            let finfo = new FileInfo(fname)
            let fileStream = new FileStream(fname, FileMode.Open, FileAccess.Read)
            let content = new StreamContent(fileStream)
            form.Add(content, "photo", finfo.Name)
            form.Add(new StringContent(cid), "chat_id")
            form.Add(new StringContent(caption), "caption")
            let! response = client.PostAsync(url, form) |> Async.AwaitTask
            fileStream.Close()
        else
            try 
                let! form = downloadFileAndPutToForm photo "photo" false 
                form.Add(new StringContent(cid), "chat_id")
                form.Add(new StringContent(title), "caption")
                let client = new HttpClient()
                let! response = client.PostAsync(url, form) |> Async.AwaitTask
                ()
            with 
            | ex -> 
                log.error "%A" ex
                do! Http.AsyncRequestString (url, query=["chat_id", cid; "photo", photo; "caption", title ], 
                        httpMethod = "POST") |> Async.Ignore

    with        
    | ex -> 
        log.error "sendPhoto %A" ex
        do! sendMessageSimple token cid caption
}


let sendFile (token: string) (cid: string) (caption:string) (document: string) = async {
    let url  = getEndpoint token "sendDocument"
    try
        let caption = if isNull(caption) then "" else caption
        let title = caption.Substring(0, min 200 caption.Length)
        try 
            let! form = downloadFileAndPutToForm document "document" false
            form.Add(new StringContent(cid), "chat_id")
            form.Add(new StringContent(title), "caption")
            let client = new HttpClient()
            let! response = client.PostAsync(url, form) |> Async.AwaitTask
            ()
        with 
        | ex -> 
            log.error "%A" ex
            do! Http.AsyncRequestString (url, query=["chat_id", cid; "document", document; "caption", title ], 
                                         httpMethod = "POST") |> Async.Ignore
    with        
    | ex -> 
        log.error "sendFile %A" ex
        do! sendMessageSimple token cid caption
}

let getFile (token: string) (file_id: string): Async<string> = async {
    let url  = getEndpoint token "getFile"
    try
        let! resp = Http.AsyncRequestString (url, query=["file_id", file_id]) 
        log.debug "getFile response: %A" resp
        if resp <> null then 
            let file = JsonDeserialize<FileResult> resp
            return "https://api.telegram.org/file/bot" + token + "/" + file.result.file_path
        else
            printfn "Null response returned"
            return ""
    with
    | ex -> 
        log.error "getFile %A" ex
        return ""
}

let getUserProfilePhotos (token: string) (user_id: string): Async<string>  = async {
    let url  = getEndpoint token "getUserProfilePhotos"
    try
        let! resp = Http.AsyncRequestString (url, query=["user_id", user_id]) 
        log.debug "getUserProfilePhotos response: %A" resp
        if resp <> null then 
            let photos = JsonDeserialize<UserProfilePhotosResult> resp
            if not(isNull photos.result) && not(isNull photos.result.photos) && (photos.result.photos.Length > 0) && (photos.result.photos.[0].Length>0) && not(isNull photos.result.photos.[0].[0]) then
                return! getFile token photos.result.photos.[0].[0].file_id
            else
                return ""
        else
            printfn "Null response returned"
            return ""
    with
    | ex -> 
        log.error "getUserProfilePhotos %A" ex
        return ""
}

let sendContacts (token: string) (cid: string) (firstname: string) (phone: string): bool =
    let url  = getEndpoint token "sendContact"
    try
        let res = Http.RequestString (url, query=["chat_id", cid; "phone_number", phone; "first_name", firstname ]) //|> ignore 
        true
    with
    | ex -> log.error "sendContacts %A" ex
            reraise()

let sendRequest (token: string) (cid: string) (msg: ReplyMessage) = async {
    try
        let url  = getEndpoint token "sendMessage"
        let request_types = [| for request_type in msg.message.request -> request_type.[0] |]
        let keyboard = 
            [|
                
                for request_type in request_types ->
                    match request_type with
                    | "phone" ->
                        [|{
                            text = "Отправить номер телефона";
                            request_contact = true
                            request_location = false
                        }|]
                    | "location" ->
                        [|{
                            text = "Отправить местоположение";
                            request_contact = false
                            request_location = true
                        }|]
                    | _ ->
                        [|{
                            text = "";
                            request_contact = false
                            request_location = false
                        }|]
                
            |]
        let row_width = 1  
        let keyboard = 
            {
                row_width = row_width;
                keyboard = keyboard
                resize_keyboard = true
                one_time_keyboard = false
            }
        let markup = JsonSerialize keyboard
        let! res = Http.AsyncRequestString (url, query = ["chat_id", cid; "text", msg.message.text; "reply_markup", markup])
        ()
    with
    | ex ->
        log.error "sendTelephoneNumberRequest %A" ex
}    

let setWebhook (token: string) (webhook_url: string) =    
    let url  = getEndpoint token "setWebhook"
    log.info "Telegram.setWebhook %A %A" url webhook_url
    try
        let res = Http.RequestString (url, query=["url", webhook_url ]) //|> ignore 
        log.info "Telegram Webhook setup successfully"
        true
    with
    | ex -> log.error "%A" ex
            false
                  

type TelegramChannel(channel: ChannelInfo, botMediatorApi: BotMediatorApi) = 
    inherit ChannelBot(channel, botMediatorApi)

    override this.start(token:string) = 
        setWebhook channel.token (channel.webhook_host + "/webhooks/telegram/" + channel.channel_id + "/" + token)    |> ignore
        uniqueBotToken <- token

    override this.send_message msg channel_user_id user_firstname user_lastname = async {
        
        log.debug "Send msg: %A" msg
        
        try 
            if msg.message_type = MessageType.Message then
                let sid = msg.session_id.ToString()

                let keyboard = 
                    if not(isNull(msg.message.actions)) && msg.message.actions.Length > 0 then
                        Some { inline_keyboard = 
                                [| 
                                    for action in msg.message.actions -> 
                                        [| {text = action.action_text; url = ""; callback_data = action.action_id}   |]
                                |]  }  
                    else
                        None

                if not(isNull(msg.message.attachment)) 
                        && not(String.IsNullOrWhiteSpace(msg.message.attachment.attachment_type))
                        && msg.message.attachment.attachment_type.StartsWith("image", StringComparison.InvariantCultureIgnoreCase) then
                    log.debug "Send an image %A %A %A" sid msg.message.text msg.message.attachment.attachment_url
                    do! sendPhoto channel.token sid "" msg.message.attachment.attachment_url
                    if not(String.IsNullOrWhiteSpace msg.message.text) then
                        log.debug "Send text %A %A " sid msg.message.text
                        if not(Array.isEmpty msg.message.request) then
                            do! sendRequest channel.token sid msg
                        else
                            do! sendMessage channel.token sid msg.message.text keyboard                    
                else if not(isNull(msg.message.attachment)) 
                        && not(String.IsNullOrWhiteSpace(msg.message.attachment.attachment_type)) then
                    log.debug "Send a file %A %A %A" sid msg.message.text msg.message.attachment.attachment_url
                    do! sendFile channel.token sid msg.message.text msg.message.attachment.attachment_url
                else
                    log.debug "Send text %A %A " sid msg.message.text
                    if not(Array.isEmpty msg.message.request) then
                        do! sendRequest channel.token sid msg
                    else
                        do! sendMessage channel.token sid msg.message.text keyboard

                this.send_service_message MessageType.SentConfirmation msg (setNull<Bots.Message>()) channel_user_id
                |> Async.RunSynchronously
            else
                ()
        with 
        |ex as Exception -> 
            log.error "Exception: %A" ex
            this.send_service_message MessageType.FailedConfirmation msg (setNull<Bots.Message>()) channel_user_id
            |> Async.RunSynchronously
    }


    override this.get_user_info user_id (user:Bots.User) sid = async {
        //do! sendTelephoneNumberRequest channel.token sid
        let! url = getUserProfilePhotos this.token user_id
        return {user with pic=url}
    }

    override this.get_location user_id = setNull<Bots.Location>()

    override this.get_link (token:string) = 
        "https://telegram.me/" + this.id_in_channel + "?start=" + token

    member this.token with get() = channel.token

let StartHost() = 
    log.info "Telegram bot started"

type TelegramWebhookController() = 
    inherit ApiController()

    [<HttpPost>]
    [<Route("webhooks/telegram/{id}/{token}")>]
    member this.Get(id:string, token:string, [<FromBody>] res:Update) =
        if not(token = uniqueBotToken) then
            this.InternalServerError() :> IHttpActionResult
        else          
            log.debug "Telegram msg: %A" res
            try 
                let msg, isaction =  
                    if isNull(res.callback_query) then 
                        if not(isNull res.message) then
                            res.message, false
                        else
                            res.edited_message, false
                    else 
                        { res.callback_query.message with text = res.callback_query.data }, true
    
                if not(isNull(msg)) then
    
                    match get_bot(id) with
                    | :? TelegramChannel as bot -> 
    
                        let attachment = 
                            if (not(isNull(msg.photo)) && msg.photo.Length > 0) then
                                let file = msg.photo |> Array.maxBy(fun p -> p.file_size)
    
                                {
                                    attachment_type = "IMAGE";
                                    attachment_url = getFile bot.token file.file_id |> Async.RunSynchronously;
                                }
                            else if not(isNull(msg.document)) then
                                {
                                    attachment_type = "FILE";
                                    attachment_url = getFile bot.token msg.document.file_id |> Async.RunSynchronously;
                                }
                            else
                            {
                                attachment_type = null; 
                                attachment_url = null 
                            }
                        
                        let message = empty_message()
                        let message = 
                            {message with
                                    id_from_channel = msg.message_id.ToString()
                                    timestamps = 
                                        {message.timestamps with    
                                            sent_from_channel = msg.date * 1000L;
                                        };
                                    channel = 
                                        {
                                            channel_id = bot.channel.channel_id;
                                            customer_id = bot.customer_id;
                                            channel_type = "telegram";
                                            id_in_channel = bot.id_in_channel;
                                            channel_features =  [| "TEXT"; "IMAGE"; "FILE" |]
                                        };
                                    user = 
                                        {message.user with
                                            channel_user_id = if isNull(msg.chat) then null else msg.chat.id.ToString();
                                            session_id = msg.chat.id.ToString();
                                            firstname = if isNull(msg.chat) then null else msg.chat.first_name;
                                            lastname = if isNull(msg.chat) then null else msg.chat.last_name;
                                            phone_number = if isNull(msg.contact) then null else msg.contact.phone_number;
                                            location = 
                                                if isNull msg.location then setNull<Bots.Location>()
                                                else
                                                { 
                                                    lat = Convert.ToDecimal(msg.location.latitude)
                                                    long = Convert.ToDecimal(msg.location.longitude)
                                                }; 
                                        };
                                    message = 
                                        {message.message with
                                            text = if not<| isNull msg.text && msg.text.StartsWith("/start") then "" else 
                                                        if isaction then "" else msg.text;
                                            attachment = attachment;
                                            action = if isaction then msg.text else null;
                                            //initial_msg = msg.text = "/start" 
                                        };
                                    message_type = 
                                        if not(String.IsNullOrWhiteSpace msg.text) && msg.text.StartsWith("/start") then 
                                            MessageType.Initial 
                                        else 
                                            message.message_type;
                                    context =
                                        if not<| isNull msg.text && msg.text.StartsWith("/start") && msg.text.Length > "/start".Length && msg.text.Substring("/start".Length).Length > 1 then
                                            [| [| "deep_linking:token" ; msg.text.Substring("/start".Length + 1) |] |]
                                        else [||]
                            }
    
                        log.debug "Converted to: %A" message
    
                        let computation = 
                            if not <| String.IsNullOrWhiteSpace message.user.phone_number then
                                async {
                                    do! bot.feed({ message with message = { message.message with text = message.user.phone_number } })
                                    do! Async.Sleep(2000)
                                    do! bot.send_service_message MessageType.PhoneNumber (setNull<ReplyMessage>()) message ""
                                }
                            elif not <| isNull message.user.location then
                                async {
                                    let text = "Latitude: " + message.user.location.lat.ToString() + "\n" + 
                                               "Longitude: " + message.user.location.long.ToString()
                                    do! bot.feed({ message with message = { message.message with text = text } })
                                    do! Async.Sleep(2000)
                                    do! bot.send_service_message MessageType.Location (setNull<ReplyMessage>()) message ""
                                }
                            else
                                bot.feed(message)
                                
                        Async.RunSynchronously computation
    
                        this.Ok() :> IHttpActionResult
                    |_ -> this.NotFound()  :> IHttpActionResult
            
                else
                    this.Ok() :> IHttpActionResult
            with
            | ex -> log.error "%A" ex
                    this.InternalServerError() :> IHttpActionResult
