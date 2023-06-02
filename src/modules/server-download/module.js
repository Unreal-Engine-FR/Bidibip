const {ModuleBase} = require("../../utils/module_base");
const DI = require("../../utils/discord_interface");
const {CommandInfo} = require("../../utils/interactionBase");
const {Channel} = require("../../utils/channel");
const fs = require("fs")
const {Message} = require("../../utils/message");
const {InteractionRow} = require("../../utils/interaction_row");
const {Button} = require("../../utils/button");
const {Embed} = require("../../utils/embed");

class Module extends ModuleBase {
    constructor(create_infos) {
        super(create_infos)
        this.enabled = false

        this.fetching_channels = {}

        this.commands = [
            new CommandInfo("download-channel", "récupère l'historique d'un channel", async (command_interaction) => {

                const channel_id = command_interaction.read('channel')

                if (this.fetching_channels[channel_id]) {
                    command_interaction.reply(new Message().set_client_only().set_text('Already fetching this channel'))
                    return
                }
                this.fetching_channels[channel_id] = true

                if (!fs.existsSync(this.app_config.SAVE_DIR + `/message_history/${channel_id}/`))
                    fs.mkdirSync(this.app_config.SAVE_DIR + `/message_history/${channel_id}/`, {recursive: true})

                const message = new Message()
                    .add_embed(new Embed().set_title('Progression...').set_description('starting...'))
                const config = {
                    stop_process: false,
                    interaction: command_interaction,
                    base_message: message
                }

                const bind_reply = (message_reply) => {
                    this.bind_button(message_reply, async (interaction) => {
                        if (interaction.button_id() === 'stop' && interaction.author().id() === command_interaction.author().id())
                            config.stop_process = true
                        await interaction.skip()
                    })
                }

                let names = []
                fs.readdirSync(this.app_config.SAVE_DIR + `/message_history/${channel_id}/`).forEach(name => names.push(name.split('.')[0]))
                names = names.sort((a, b) => a - b)
                if (names.length === 0) {
                    message.set_text(`Downloading messages of ${new Channel().set_id(channel_id).url()}`)
                    message.add_interaction_row(new InteractionRow()
                        .add_button(new Button()
                            .set_id('stop').set_label('Stop').set_type(Button.Danger)))
                    bind_reply(await command_interaction.reply(message))
                    this.fetch_channel(command_interaction.read('channel'), 0, config)
                        .catch(err => console.fatal(`Failed to fetch channel : ${err}`))
                } else {
                    const last_file = names[names.length - 1]
                    const last_file_content = JSON.parse(fs.readFileSync(this.app_config.SAVE_DIR + `/message_history/${channel_id}/${last_file}.json`, 'utf-8'))
                    const last_file_ids = Object.keys(last_file_content).sort((a, b) => a - b)
                    const last_message = last_file_content[last_file_ids[0]]
                    message.set_text(`Downloading messages of ${new Channel().set_id(channel_id).url()} from ${new Message().set_id(last_message.i).set_channel(new Channel().set_id(channel_id)).url()}`)
                    message.add_interaction_row(new InteractionRow()
                        .add_button(new Button()
                            .set_id('stop').set_label('Stop').set_type(Button.Danger)))
                    bind_reply(await command_interaction.reply(message))
                    this.fetch_channel(command_interaction.read('channel'), Number(last_file) + 1, config, last_message.i)
                        .catch(err => console.fatal(`Failed to fetch channel : ${err}`))
                }

            })
                .add_channel_option("channel", "download target")
                .set_admin_only()
        ]
    }

    async fetch_messages(discord_handle, before_id = null) {
        if (before_id)
            return await discord_handle.messages.fetch({before: before_id})
                .catch(console.fatal)
        else
            return await discord_handle.messages.fetch()
                .catch(console.fatal)
    }

    async fetch_channel(channel_id, out_file, config, from = null) {
        const _handle = await new Channel().set_id(channel_id)._fetch_from_discord()
            .catch(console.fatal)
        let last_message = from

        let message_count = 0
        let finished = false
        while (!config.stop_process && !finished) {
            let step_messages = 0
            const json = {}
            for (let i = 0; i < 10 && !config.stop_process && !finished; ++i) {
                finished = true
                let messages = await this.fetch_messages(_handle, last_message)
                    .catch(err => console.fatal(`Failed to fetch messages : ${err}`))
                messages.forEach(msg => {
                    finished = false
                    const data = {
                        i: msg.id,
                        t: msg.content,
                        u: msg.author.username + "#" + msg.author.discriminator
                    }
                    const attachments = []
                    for (const [_, v] of msg.attachments)
                        attachments.push({n: v.name, f: v.attachment})
                    if (attachments.length !== 0)
                        data.a = attachments

                    json[msg.createdTimestamp] = data

                    message_count += 1
                    step_messages += 1
                })
                if (messages.last())
                    last_message = messages.last().id
            }

            if (step_messages !== 0) {
                console.log(`Saved ${step_messages} messages to ${out_file}.json`)
                fs.writeFileSync(`${this.app_config.SAVE_DIR}/message_history/${channel_id}/${out_file}.json`, JSON.stringify(json))
            }

            config.base_message.embeds()[0].set_description(`Fetched ${step_messages} messages to ${out_file}.json out of ${message_count} total messages ! (last message : ${new Message().set_id(last_message).set_channel(new Channel().set_id(channel_id)).url()})`)
            config.interaction.edit_reply(config.base_message)
                .catch(err => console.fatal(`Failed to edit reply : ${err}`))
            out_file += 1
        }

        await config.interaction.edit_reply(config.base_message.add_embed(
            new Embed().set_title('Message download operation finished !')
                .set_description(`${message_count} messages downloaded`)
                .add_field("Status", finished ? "finished" : config.stop_process ? "stopped by user" : "undefined"))
            .clear_interactions())
            .catch(err => console.fatal(`Failed to edit reply : ${err}`))
        delete this.fetching_channels[channel_id]
    }
}

module.exports = {Module}