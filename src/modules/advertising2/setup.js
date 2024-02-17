const {Button} = require("../../utils/button");
const {Message} = require("../../utils/message");
const {InteractionRow} = require("../../utils/interaction_row");
const {hire} = require("./mode_hire_freelance");

class AdvertisingSetup {
    /**
     * @param ctx {ModuleBase}
     * @param thread {Thread}
     * @param author {User}
     */
    constructor(ctx, thread, author) {
        this.thread = thread;
        this.author = author;
        this.ctx = ctx;

        this.start()
            .catch((error) => {
                console.fatal(`Announcement failed : ${error}`)
            })
        ctx.advertising_setups[thread.id] = this;
    }

    KIND_FREELANCE = { type: 0, text: "Freelance"};
    KIND_UNPAID = { type: 0, text: "Bénévolat (non rémunéré)"};
    KIND_INTERN_FREE = { type: 0, text: "Stage (non rémunéré)"};
    KIND_INTERN_PAID = { type: 0, text: "Stage (rémunéré)"};
    KIND_WORK_STUDY = { type: 0, text: "Alternance (rémunéré)"};
    KIND_PAID_LIMITED = { type: 0, text: "CDD (rémunéré)"};
    KIND_PAID_UNLIMITED = { type: 0, text: "CDI (rémunéré)"};

    async start() {
        switch (await this.askChoice('Que cherches tu ?', ['Je cherche du travail', 'Je recrute'])) {
            case 0:
                await hire(this, await this.askChoice('Quel type de contrat recherches-tu ?', ['Freelance', 'Bénévolat (non rémunéré)', '']));
                break;
            case 1:
                await hire(this, await this.askChoice('Quel type de contrat souhaites-tu ?', ['Freelance', 'Bénévolat (non rémunéré)', 'rémunéré']));
                break;
            case null:
                console.fatal('Invalid response');
                break;
            default:
                console.fatal(`Unhandled response`);
        }
    }

    async askChoice(text, questions) {
        const interaction = new InteractionRow();
        for (const i in questions)
            interaction.add_button(new Button().set_id(questions[i]).set_label(questions[i]));
        const message = await this.thread.send(
            new Message()
                .set_text(text)
                .add_interaction_row(interaction));

        return await new Promise((resolve) => {
            this.ctx.bind_button(message, async (interaction) => {
                let selected = null;
                for (const i in questions) {
                    const id = questions[i];
                    if (interaction.button_id() === id) {
                        (await message.get_button_by_id(id)).set_type(Button.Success).set_enabled(false);
                        selected = Number(i);
                    } else
                        (await message.get_button_by_id(id)).set_type(Button.Secondary).set_enabled(false);
                }
                await message.update(message)
                await interaction.skip();
                resolve(selected);
            })
        });
    }

    async askUser(question, required = true) {
        const message = new Message()
            .set_text(question);
        if (!required) {
            message.add_interaction_row(new InteractionRow().add_button(new Button().set_id("no").set_label("Pas concerné").set_type(Button.Secondary)));
        }
        const sent = await this.thread.send(message);

        return await new Promise((resolve) => {
            if (!required) {
                this.ctx.bind_button(sent, async (interaction) => {
                    if (interaction.button_id() === 'no') {
                        await interaction.skip();
                        resolve(null);
                    }
                })
            }
            this.waiting_message = async (text) => {
                resolve(text);
            }
        });
    }

    /**
     * @param message {Message}
     */
    async received_message(message) {
        if (this.waiting_message) {
            await this.waiting_message(await message.text())
            delete this.waiting_message;
        }
    }
}

module.exports = {AdvertisingSetup}