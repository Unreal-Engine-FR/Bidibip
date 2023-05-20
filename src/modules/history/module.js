// MODULE HISTORY
const CONFIG = require("../../config").get()
const {Message} = require('../../utils/message')
const {Embed} = require('../../utils/embed')

class Module {
    constructor(create_infos) {
        this.enabled = true // default value is true

        this.client = create_infos.client
    }

    async server_message_updated(old_message, new_message) {
        console.info(`Message updated [${old_message.author.full_name}] :\n${old_message.text}\nto\n${new_message.text}`)

        await new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
            .add_embed(
                new Embed()
                    .set_description('Message modifié :')
                    .set_title(old_message.author.full_name)
                    .add_field('ancien', old_message.text)
                    .add_field('nouveau', new_message.text)
            ).send()
    }

    async server_message_delete(message) {
        console.info(`Message deleted [${message.author.full_name}] :\n${message.text}`)
        await new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
            .add_embed(
                new Embed()
                    .set_description('Message supprimé :')
                    .set_title(message.author.full_name)
                    .add_field('Contenu', message.text)
            ).send()
    }
}

module.exports = {Module}