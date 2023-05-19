// MODULE HISTORY
const {CommandInfo, Message, Embed} = require("../../discord_interface");
const CONFIG = require("../../config").get()

class Module {
    constructor(create_infos) {
        this.enabled = true // default value is true

        this.client = create_infos.client
    }

    server_message_updated(old_message, new_message) {
        console.info(`Message updated [${old_message.author.name}] :\n${old_message.text}\nto\n${new_message.text}`)
        this.client.say(
            new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
                .add_embed(
                    new Embed()
                        .set_description('Message modifié :')
                        .set_title(old_message.author.name)
                        .add_field('ancien', old_message.text)
                        .add_field('nouveau', new_message.text)
                )
        )
    }

    server_message_delete(message) {
        console.info(`Message deleted [${message.author.name}] :\n${message.text}`)
        this.client.say(
            new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
                .add_embed(
                    new Embed()
                        .set_description('Message supprimé :')
                        .set_title(message.author.name)
                        .add_field('Contenu', message.text)
                )
        )
    }
}

module.exports = {Module}