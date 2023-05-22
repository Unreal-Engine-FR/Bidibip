// MODULE HISTORY
const CONFIG = require("../../config").get()
const {Message} = require('../../utils/message')
const {Embed} = require('../../utils/embed')

class Module {
    constructor(create_infos) {
        this.enabled = true // default value is true

        this.client = create_infos.client
    }

    /**
     * @param old_message {Message}
     * @param new_message {Message}
     * @return {Promise<void>}
     */
    async server_message_updated(old_message, new_message) {
        const author = await old_message.author()
        console.info(`Message updated [${await author.full_name()}] :\n${await old_message.text()}\nto\n${await new_message.text()}`)
        await new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
            .add_embed(
                new Embed()
                    .set_title(`@${await author.full_name()} (${author.id()})`)
                    .set_description('Message modifié :')
                    .add_field('ancien', await old_message.text())
                    .add_field('nouveau', await new_message.text())
            ).send().catch(err => console.error(`failed to send log message : ${err}`))
    }

    async server_message_delete(message) {
        const author = await message.author()
        console.info(`Message deleted [${await author.full_name()}] :\n${await message.text()}`)
        await new Message().set_channel(CONFIG.LOG_CHANNEL_ID)
            .add_embed(
                new Embed()
                    .set_title(`@${await author.full_name()} (${author.id()})`)
                    .set_description('Message supprimé :')
                    .add_field('Contenu', await message.text())
            ).send()
    }
}

module.exports = {Module}