// MODULE ADVERTISING2
const {CommandInfo} = require("../../utils/interactionBase")
const {Message} = require('../../utils/message')
const {Channel} = require("../../utils/channel");
const {ModuleBase} = require("../../utils/module_base");
const {Thread} = require("../../utils/thread.js");
const {InteractionRow} = require("../../utils/interaction_row");
const {Button} = require("../../utils/button");
const HIRE = require("./hire")
const APPLY = require("./hire")

class Module extends ModuleBase {
    constructor(create_infos) {
        super(create_infos)
        this.client = create_infos.client

        this.commands = [
            new CommandInfo('annonce', 'Créer une annonce d\'offre ou de recherche d\'emploi', this.annonce)
                .set_member_only(),
        ]
    }

    /**
     * @param command_interaction {CommandInteraction}
     * @return {Promise<void>}
     */
    async annonce(command_interaction) {
        let thread_channel = new Channel().set_id(command_interaction.channel().id());
        if (await thread_channel.type() === Channel.TypeThread || await thread_channel.type() === Channel.TypePrivateThread) {
            thread_channel = new Channel().set_id(this.app_config.TICKET_CHANNEL_ID);
        }
        const user = await command_interaction.author();
        const thread = await new Thread().set_id(await thread_channel.create_thread(`Annonce de ${await user.name()}`, true)
            .catch(err => console.fatal(`Failed to create thread for announcement : ${err}`)));

        const response = await thread.send(
            new Message()
                .set_text(`Bienvenue dans le formulaire de création d'annonce ${(user.mention())}.\n Quel type d'annonce souhaites tu publier ?`)
                .add_interaction_row(
                    new InteractionRow()
                        .add_button(new Button().set_id("hire.js").set_label("Je recrute"))
                        .add_button(new Button().set_id("apply").set_label("Je cherche du travail"))));

        this.bind_button(response, async (interaction) => {
            if (interaction.button_id() === 'hire.js') {
                (await response.get_button_by_id('hire.js')).set_type(Button.Success).set_enabled(false);
                (await response.get_button_by_id('apply')).set_type(Button.Secondary).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await this.mode_hire(thread, user);
            }
            else if (interaction.button_id() === 'apply') {
                (await response.get_button_by_id('hire.js')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('apply')).set_type(Button.Success).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await this.mode_apply(thread, user);
            }
        })

        await command_interaction.reply(new Message().set_client_only().set_text(`Bien reçu, la suite se passe ici :arrow_right: ${thread.url()}`))
    }

    /**
     * @param thread {Thread}
     * @param owner {User}
     */
    async mode_hire(thread, owner) {
        const response = await thread.send(
            new Message()
                .set_text(`Quel type de contrat proposes-tu ?`)
                .add_interaction_row(
                    new InteractionRow()
                        .add_button(new Button().set_id("freelance").set_label("Freelance"))
                        .add_button(new Button().set_id("unpaid").set_label("Coopération (non rémunéré)"))
                        .add_button(new Button().set_id("paid").set_label("Contrat rémunéré"))
                ));

        this.bind_button(response, async (interaction) => {
            if (interaction.button_id() === 'freelance') {
                (await response.get_button_by_id('freelance')).set_type(Button.Success).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Secondary).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await HIRE.freelance(thread, owner)
            }
            else if (interaction.button_id() === 'unpaid') {
                (await response.get_button_by_id('freelance')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Success).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Secondary).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await HIRE.unpaid(thread, owner)
            }
            else if (interaction.button_id() === 'paid') {
                (await response.get_button_by_id('freelance')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Success).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await HIRE.paid(thread, owner)
            }
        })
    }

    /**
     * @param thread {Thread}
     * @param owner {User}
     */
    async mode_apply(thread, owner) {
        const response = await thread.send(
            new Message()
                .set_text(`Quel type de contrat cherches-tu ?`)
                .add_interaction_row(
                    new InteractionRow()
                        .add_button(new Button().set_id("freelance").set_label("Freelance"))
                        .add_button(new Button().set_id("unpaid").set_label("Coopération (non rémunéré)"))
                        .add_button(new Button().set_id("paid").set_label("Contrat rémunéré"))
                ));

        this.bind_button(response, async (interaction) => {
            if (interaction.button_id() === 'freelance') {
                (await response.get_button_by_id('freelance')).set_type(Button.Success).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Secondary).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await APPLY.freelance(thread, owner)
            }
            else if (interaction.button_id() === 'unpaid') {
                (await response.get_button_by_id('freelance')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Success).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Secondary).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await APPLY.unpaid(thread, owner)
            }
            else if (interaction.button_id() === 'paid') {
                (await response.get_button_by_id('freelance')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('unpaid')).set_type(Button.Secondary).set_enabled(false);
                (await response.get_button_by_id('paid')).set_type(Button.Success).set_enabled(false);
                await response.update(response);
                await interaction.skip();
                await APPLY.paid(thread, owner)
            }
        })
    }
}

module.exports = {Module}