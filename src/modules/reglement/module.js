const {ModuleBase} = require("../../utils/module_base");
const {Channel} = require("../../utils/channel");
const {Message} = require("../../utils/message");
const {InteractionRow} = require("../../utils/interaction_row");
const {Button} = require("../../utils/button");
const {User} = require("../../utils/user");
const {Embed} = require("../../utils/embed");
const {json_to_message} = require("../../utils/json_to_message");


class Module extends ModuleBase {
    constructor(create_infos) {
        super(create_infos);

        const approval_message = new Message()
            .set_channel(new Channel().set_id(this.app_config.REGLEMENT_CHANNEL_ID))
            .set_id(this.module_config.approval)

        approval_message.is_valid().then(async result => {
                if (result)
                    await this.bind_approval(approval_message)
                else
                    console.error('No approval message is configured')
            }
        )
    }


    /**
     * @param message {Message}
     * @return {Promise<void>}
     */
    async server_message(message) {
        if (message.channel().id() === this.app_config.REGLEMENT_CHANNEL_ID && message.attachments().length !== 0 && message.attachments()[0].name() === "reglement.json") {

            json_to_message(await message.attachments()[0].get_content())
                .then(async messages => {
                    for (const to_send of messages) {
                        await to_send.set_channel(new Channel().set_id(this.app_config.REGLEMENT_CHANNEL_ID)).send()
                            .then(sent => {
                                this.module_config.approval = sent.id()
                                this.save_config()
                                this.bind_approval(sent)
                            })
                            .catch(err => console.fatal(`Failed to send reglement message : ${err}`))
                    }
                })
                .catch(async error => {
                    await (await message.author()).send(new Message()
                        .set_text('Echec de la conversion du message')
                        .add_embed(new Embed().set_title("Raison").set_description(`${error}`)))
                })
        }
    }

    /**
     * @param message {Message}
     * @return {Promise<void>}
     */
    async bind_approval(message) {
        if (message.get_button_by_id('approval')) {
            this.bind_button(message, async (interaction) => {
                if (interaction.button_id() === 'approval') {
                    await interaction.author().add_role(this.app_config.MEMBER_ROLE_ID)
                    await interaction.reply(new Message().set_text(`Bienvenue sur le serveur ${interaction.author().mention()} ! Tu as pris connaissance du réglement.`).set_client_only())
                } else
                    await interaction.skip()
            })
        }
    }
}

module
    .exports = {Module}